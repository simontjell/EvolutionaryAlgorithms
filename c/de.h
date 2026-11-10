/*
 * de.h - single-header, performance-focused C implementation of the
 * Differential Evolution algorithm from the C# EvolutionaryAlgorithm /
 * DifferentialEvolution libraries (DE/rand/1/bin + Pareto survival +
 * NSGA-II truncation).
 *
 * Observable behaviour (selection distribution, survival rules, Pareto
 * ranks, truncation order) matches the C# version. What differs is *how*
 * it is computed:
 *
 *  - Structure-of-arrays storage: genes and fitness values live in flat,
 *    contiguous double arrays; individuals are row indices. All memory is
 *    allocated once in de_init(); the generation loop never allocates.
 *  - Parent and trial vectors share one buffer, so the survivor "pool" is
 *    just a list of row indices; only the n survivors are copied per
 *    generation (into a double buffer that is then swapped).
 *  - Parent triple selection (DE_SELECT_SAMPLE, default): the C# code
 *    shuffles all other parents (`OrderBy(_ => random.NextDouble())`) and
 *    takes three: O(n log n) per offspring. Taking the first three of a
 *    uniformly random permutation is the same as drawing an ordered triple
 *    of distinct indices uniformly, which is done here in O(1).
 *    DE_SELECT_SHUFFLE keeps the original O(n log n) shuffle for comparison.
 *  - Feasibility is checked before evaluation, so infeasible trials are
 *    never evaluated (C# evaluates first, then discards them).
 *  - Pareto ranking uses ENS-BS (Zhang et al., 2015: "An Efficient Approach
 *    to Nondominated Sorting for Evolutionary Multiobjective Optimization")
 *    instead of Deb's O(M N^2) fast non-dominated sort. It yields the same
 *    fronts; with two objectives each front check is a single comparison,
 *    giving O(N log N). Single-objective ranks are dense ranks from the
 *    sort that truncation needs anyway.
 *  - Sorting is a stable merge sort on 64-bit order-preserving integer keys
 *    (same ordering as .NET's Comparer<double>.Default, NaN first), so ties
 *    resolve exactly like LINQ's stable OrderBy.
 *  - No generation history is kept (C# and the Rust port store every
 *    generation); use the per-generation callback to observe progress.
 *  - PRNG: xoshiro256+ (C# uses System.Random). The crossover test
 *    `rand < CR` is done on raw 64-bit integers.
 */
#ifndef DE_H
#define DE_H

#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#if defined(__GNUC__) || defined(__clang__)
#define DE_INLINE static inline __attribute__((always_inline))
#else
#define DE_INLINE static inline
#endif

/* ------------------------------------------------------------------ RNG */

typedef struct { uint64_t s[4]; } de_rng;

DE_INLINE uint64_t de_rotl(uint64_t x, int k) { return (x << k) | (x >> (64 - k)); }

static inline void de_rng_seed(de_rng *r, uint64_t seed)
{
    for (int i = 0; i < 4; i++) { /* SplitMix64 */
        uint64_t z = (seed += 0x9E3779B97F4A7C15ULL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ULL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBULL;
        r->s[i] = z ^ (z >> 31);
    }
}

DE_INLINE uint64_t de_rng_u64(de_rng *r) /* xoshiro256+ */
{
    uint64_t *s = r->s;
    const uint64_t result = s[0] + s[3];
    const uint64_t t = s[1] << 17;
    s[2] ^= s[0];
    s[3] ^= s[1];
    s[1] ^= s[2];
    s[0] ^= s[3];
    s[2] ^= t;
    s[3] = de_rotl(s[3], 45);
    return result;
}

/* Uniform double in [0, 1). */
DE_INLINE double de_rng_f64(de_rng *r) { return (double)(de_rng_u64(r) >> 11) * 0x1.0p-53; }

/* Uniform integer in [0, n) (Lemire multiply-shift). */
DE_INLINE uint32_t de_rng_below(de_rng *r, uint32_t n)
{
    return (uint32_t)(((unsigned __int128)de_rng_u64(r) * n) >> 64);
}

/* ------------------------------------------------------------- problem */

typedef void (*de_eval_fn)(const double *x, double *f, int dims, const void *ctx);
typedef bool (*de_feasible_fn)(const double *x, int dims, const void *ctx);
typedef void (*de_init_fn)(double *x, int dims, de_rng *rng, const void *ctx);

typedef struct {
    int dims;
    int objectives;
    de_eval_fn eval;         /* required */
    de_feasible_fn feasible; /* NULL: everything is feasible */
    de_init_fn init;         /* NULL: genes uniform in [0, 1) */
    const void *ctx;
} de_problem;

/* ---------------------------------------------------------- parameters */

typedef enum { DE_SELECT_SAMPLE = 0, DE_SELECT_SHUFFLE = 1 } de_selection;

typedef struct {
    int population;          /* >= 4 */
    double cr;               /* crossover rate, C# default 0.5 */
    double f;                /* differential weight, C# default 1.0 */
    int generations;         /* stop when this many generations exist (incl. the initial one) */
    de_selection selection;
    uint64_t seed;
    const double *injected;  /* optional: n_injected * dims genes placed in the first generation */
    int n_injected;
} de_params;

#define DE_DEFAULT_CR 0.5
#define DE_DEFAULT_F 1.0

/* --------------------------------------------------------------- state */

typedef struct de_state {
    /* public, read-only after de_init */
    int n, d, m;
    int generation_count;    /* generations produced so far, incl. the initial one */
    double *x;               /* population genes: row i at x + i*d, rows 0..n-1 */
    double *fit;             /* population fitness: row i at fit + i*m */
    uint32_t *rank;          /* Pareto rank (front index) of each individual */

    /* private */
    double cr, f;
    uint64_t cr_threshold;
    int max_generations;
    de_selection selection;
    de_rng rng;
    const double *injected;
    int n_injected;
    double *nx, *nfit;       /* the other half of the double buffer (2n rows each) */
    uint32_t *nrank;
    uint32_t *pool;          /* survivor pool: row indices into x/fit (rows n..2n-1 are trials) */
    uint32_t *pool_rank;
    uint32_t *order, *selected, *counts;
    uint64_t *keys, *tkeys;
    uint32_t *idx, *tidx;
    uint32_t *front_tail, *prev;
    double *dist;
} de_state;

typedef int (*de_callback)(const de_state *s, void *user); /* return non-zero to stop */

/* -------------------------------------------------------- sort helpers */

/* Order-preserving map double -> uint64 matching .NET Comparer<double>.Default:
 * NaN < -inf < ... < -0 == +0 < ... < +inf. */
DE_INLINE uint64_t de_key(double v)
{
    if (v != v) return 0;
    if (v == 0.0) v = 0.0;
    uint64_t b;
    memcpy(&b, &v, sizeof b);
    return (b >> 63) ? ~b : (b | 0x8000000000000000ULL);
}

/* Stable sort of (k, v) pairs by k ascending; tk/tv are scratch of size n.
 * Insertion sort for small inputs, otherwise LSD radix sort (8-bit digits)
 * that skips digits on which all keys agree - for doubles of similar
 * magnitude the sign/exponent bytes are usually constant. */
static void de_sort(uint64_t *k, uint32_t *v, uint32_t n, uint64_t *tk, uint32_t *tv)
{
    if (n < 48) {
        for (uint32_t i = 1; i < n; i++) {
            uint64_t key = k[i];
            uint32_t val = v[i];
            uint32_t j = i;
            while (j > 0 && k[j - 1] > key) { k[j] = k[j - 1]; v[j] = v[j - 1]; j--; }
            k[j] = key; v[j] = val;
        }
        return;
    }
    uint32_t hist[8][256];
    memset(hist, 0, sizeof hist);
    for (uint32_t i = 0; i < n; i++) {
        const uint64_t key = k[i];
        for (int b = 0; b < 8; b++) hist[b][(key >> (8 * b)) & 0xFF]++;
    }
    uint64_t *sk = k, *dk = tk;
    uint32_t *sv = v, *dv = tv;
    for (int b = 0; b < 8; b++) {
        uint32_t *h = hist[b];
        if (h[(sk[0] >> (8 * b)) & 0xFF] == n) continue; /* all keys share this digit */
        uint32_t sum = 0;
        for (int i = 0; i < 256; i++) { uint32_t c = h[i]; h[i] = sum; sum += c; }
        for (uint32_t i = 0; i < n; i++) {
            const uint32_t pos = h[(sk[i] >> (8 * b)) & 0xFF]++;
            dk[pos] = sk[i];
            dv[pos] = sv[i];
        }
        uint64_t *t1 = sk; sk = dk; dk = t1;
        uint32_t *t2 = sv; sv = dv; dv = t2;
    }
    if (sk != k) { memcpy(k, sk, n * sizeof *k); memcpy(v, sv, n * sizeof *v); }
}

/* ----------------------------------------------------------- dominance */

DE_INLINE bool de_dominates(const double *a, const double *b, int m)
{
    bool better = false;
    for (int i = 0; i < m; i++) {
        double diff = a[i] - b[i];
        if (diff > 0) return false;
        if (diff < 0) better = true;
    }
    return better;
}

/* Reference ranking by repeated peeling of non-dominated sets (O(F M N^2)).
 * Used as a fallback when NaNs are present and as a test oracle. */
static void de_rank_peel(const double *fit, int m, const uint32_t *rows, uint32_t cnt,
                         uint32_t *rank_out, uint32_t *scratch)
{
    uint32_t *left = scratch, nleft = cnt;
    for (uint32_t i = 0; i < cnt; i++) { left[i] = i; rank_out[i] = UINT32_MAX; }
    for (uint32_t r = 0; nleft > 0; r++) {
        for (uint32_t a = 0; a < nleft; a++) {
            const double *fa = fit + (size_t)rows[left[a]] * m;
            bool dominated = false;
            for (uint32_t b = 0; b < nleft && !dominated; b++)
                dominated = b != a && de_dominates(fit + (size_t)rows[left[b]] * m, fa, m);
            if (!dominated) rank_out[left[a]] = r;
        }
        uint32_t k = 0;
        for (uint32_t a = 0; a < nleft; a++)
            if (rank_out[left[a]] == UINT32_MAX) left[k++] = left[a];
        if (k == nleft) break; /* cyclic dominance (only possible with NaN): stop */
        nleft = k;
    }
}

/*
 * ENS-BS non-dominated sorting. rows[0..cnt) are row indices into fit;
 * rank_out[i] receives the front index of rows[i]. Same result as Deb's
 * fast non-dominated sort whenever no objective value is NaN.
 */
static void de_rank_fronts(const double *fit, int m, const uint32_t *rows, uint32_t cnt, uint32_t *rank_out,
                           uint64_t *keys, uint32_t *idx, uint64_t *tk, uint32_t *tv,
                           uint32_t *front_tail, uint32_t *prev)
{
    /* NaN breaks the lexicographic argument below; fall back to peeling. */
    for (uint32_t i = 0; i < cnt; i++)
        for (int o = 0; o < m; o++) {
            double v = fit[(size_t)rows[i] * m + o];
            if (v != v) { de_rank_peel(fit, m, rows, cnt, rank_out, idx); return; }
        }

    /* Lexicographic order by (f0, f1, ..., f_{m-1}) via LSD stable sorts. */
    for (uint32_t i = 0; i < cnt; i++) idx[i] = i;
    for (int o = m - 1; o >= 0; o--) {
        for (uint32_t i = 0; i < cnt; i++) keys[i] = de_key(fit[(size_t)rows[idx[i]] * m + o]);
        de_sort(keys, idx, cnt, tk, tv);
    }

    /* In lexicographic order nobody can dominate an earlier solution, so
     * each solution goes to the first front that does not dominate it.
     * "Dominated by front k" is monotone in k, hence binary search. */
    uint32_t nfronts = 0;
    for (uint32_t s = 0; s < cnt; s++) {
        uint32_t p = idx[s];
        const double *fp = fit + (size_t)rows[p] * m;
        uint32_t lo = 0, hi = nfronts;
        while (lo < hi) {
            uint32_t mid = (lo + hi) >> 1;
            bool dominated = false;
            if (m == 2) {
                /* Within a front f1 is non-increasing in lexicographic order,
                 * so the last member is the only candidate dominator. */
                dominated = de_dominates(fit + (size_t)rows[front_tail[mid]] * m, fp, 2);
            } else {
                for (uint32_t q = front_tail[mid]; q != UINT32_MAX && !dominated; q = prev[q])
                    dominated = de_dominates(fit + (size_t)rows[q] * m, fp, m);
            }
            if (dominated) lo = mid + 1; else hi = mid;
        }
        if (lo == nfronts) { prev[p] = UINT32_MAX; nfronts++; }
        else prev[p] = front_tail[lo];
        front_tail[lo] = p;
        rank_out[p] = lo;
    }
}

/* Crowding distance of the individuals rows[group[0..len)] (Deb et al. 2002, III-B). */
static void de_crowding(const double *fit, int m, const uint32_t *rows, const uint32_t *group, uint32_t len,
                        double *dist, uint64_t *keys, uint32_t *idx, uint64_t *tk, uint32_t *tv)
{
    if (len <= 2) {
        for (uint32_t i = 0; i < len; i++) dist[i] = __builtin_inf();
        return;
    }
    for (uint32_t i = 0; i < len; i++) dist[i] = 0.0;
#define DE_F(i) fit[(size_t)rows[group[(i)]] * m + o]
    for (int o = 0; o < m; o++) {
        for (uint32_t i = 0; i < len; i++) { idx[i] = i; keys[i] = de_key(DE_F(i)); }
        de_sort(keys, idx, len, tk, tv);
        double lo = DE_F(idx[0]), hi = DE_F(idx[len - 1]);
        double range = hi - lo;
        dist[idx[0]] = __builtin_inf();
        dist[idx[len - 1]] = __builtin_inf();
        if (range > 0) {
            for (uint32_t i = 1; i < len - 1; i++)
                if (dist[idx[i]] != __builtin_inf())
                    dist[idx[i]] += (DE_F(idx[i + 1]) - DE_F(idx[i - 1])) / range;
        }
    }
#undef DE_F
}

/* ------------------------------------------------------- DE operators */

/* Uniform ordered triple of distinct indices in [0, n) \ {i}. */
DE_INLINE void de_sample3(de_rng *r, uint32_t n, uint32_t i, uint32_t *a, uint32_t *b, uint32_t *c)
{
    uint32_t x = de_rng_below(r, n - 1);
    x += x >= i;
    uint32_t e0 = i < x ? i : x, e1 = i < x ? x : i;
    uint32_t y = de_rng_below(r, n - 2);
    y += y >= e0;
    y += y >= e1;
    uint32_t s0 = e0, s1 = e1, s2 = y; /* sort the three exclusions */
    if (s2 < s1) { s2 = s1; s1 = y; if (s1 < s0) { s1 = s0; s0 = y; } }
    uint32_t z = de_rng_below(r, n - 3);
    z += z >= s0;
    z += z >= s1;
    z += z >= s2;
    *a = x; *b = y; *c = z;
}

/* ------------------------------------------------------------ lifecycle */

static inline void de_free(de_state *s)
{
    void *bufs[] = {s->x, s->fit, s->rank, s->nx, s->nfit, s->nrank, s->pool, s->pool_rank, s->order,
                    s->selected, s->counts, s->keys, s->tkeys, s->idx, s->tidx, s->front_tail, s->prev, s->dist};
    for (size_t i = 0; i < sizeof bufs / sizeof *bufs; i++) free(bufs[i]);
    memset(s, 0, sizeof *s);
}

/* Change the crossover rate and differential weight; takes effect from the next de_step(). */
static inline void de_set_rates(de_state *s, double cr, double f)
{
    s->cr = cr;
    s->f = f;
    s->cr_threshold = cr >= 1.0 ? UINT64_MAX : cr <= 0.0 ? 0 : (uint64_t)(cr * 0x1.0p64);
}

/* Change the donor selection method; takes effect from the next de_step(). */
static inline void de_set_selection(de_state *s, de_selection selection) { s->selection = selection; }

/* Returns 0 on success, -1 on invalid parameters or allocation failure. */
static inline int de_init(de_state *s, const de_problem *p, const de_params *prm)
{
    memset(s, 0, sizeof *s);
    if (prm->population < 4 || p->dims < 1 || p->objectives < 1 || !p->eval ||
        prm->n_injected < 0 || prm->n_injected > prm->population)
        return -1;
    const size_t n = (size_t)prm->population, d = (size_t)p->dims, m = (size_t)p->objectives, n2 = 2 * n;
    s->n = (int)n; s->d = (int)d; s->m = (int)m;
    de_set_rates(s, prm->cr, prm->f);
    s->max_generations = prm->generations;
    s->selection = prm->selection;
    s->injected = prm->injected;
    s->n_injected = prm->n_injected;
    de_rng_seed(&s->rng, prm->seed);

    s->x = malloc(n2 * d * sizeof(double));      s->nx = malloc(n2 * d * sizeof(double));
    s->fit = malloc(n2 * m * sizeof(double));    s->nfit = malloc(n2 * m * sizeof(double));
    s->rank = malloc(n * sizeof(uint32_t));      s->nrank = malloc(n * sizeof(uint32_t));
    s->pool = malloc(n2 * sizeof(uint32_t));     s->pool_rank = malloc(n2 * sizeof(uint32_t));
    s->order = malloc(n2 * sizeof(uint32_t));    s->selected = malloc(n2 * sizeof(uint32_t));
    s->counts = malloc((n2 + 1) * sizeof(uint32_t));
    s->keys = malloc(n2 * sizeof(uint64_t));     s->tkeys = malloc(n2 * sizeof(uint64_t));
    s->idx = malloc(n2 * sizeof(uint32_t));      s->tidx = malloc(n2 * sizeof(uint32_t));
    s->front_tail = malloc(n2 * sizeof(uint32_t)); s->prev = malloc(n2 * sizeof(uint32_t));
    s->dist = malloc(n2 * sizeof(double));
    if (!s->x || !s->nx || !s->fit || !s->nfit || !s->rank || !s->nrank || !s->pool || !s->pool_rank ||
        !s->order || !s->selected || !s->counts || !s->keys || !s->tkeys || !s->idx || !s->tidx ||
        !s->front_tail || !s->prev || !s->dist) {
        de_free(s);
        return -1;
    }
    return 0;
}

/* ------------------------------------------------------------ the loop */

/* de_run() = de_initialize() + de_step() until done. Call the two directly
 * to drive the optimisation one generation at a time (e.g. from a UI). */

DE_INLINE void de_initialize(de_state *s, const de_problem *p)
{
    const int n = s->n, d = s->d, m = s->m;
    if (s->n_injected) memcpy(s->x, s->injected, (size_t)s->n_injected * d * sizeof(double));
    for (int i = s->n_injected; i < n; i++) {
        double *xi = s->x + (size_t)i * d;
        if (p->init) p->init(xi, d, &s->rng, p->ctx);
        else for (int j = 0; j < d; j++) xi[j] = de_rng_f64(&s->rng);
    }
    for (int i = 0; i < n; i++) p->eval(s->x + (size_t)i * d, s->fit + (size_t)i * m, d, p->ctx);
    /* As in C#: the initial "rank" is the number of individuals dominating it. */
    for (int i = 0; i < n; i++) {
        uint32_t c = 0;
        for (int j = 0; j < n; j++) c += de_dominates(s->fit + (size_t)j * m, s->fit + (size_t)i * m, m);
        s->rank[i] = c;
    }
    s->generation_count = 1;
}

DE_INLINE void de_step(de_state *s, const de_problem *p)
{
    const uint32_t n = (uint32_t)s->n;
    const int d = s->d, m = s->m;
    double *restrict x = s->x;
    double *restrict fit = s->fit;
    const double F = s->f;
    const uint64_t thr = s->cr_threshold;
    de_rng r = s->rng; /* keep the generator in registers */

    /* 1. Trial vectors: rows n..2n-1 (DE/rand/1/bin). */
    for (uint32_t i = 0; i < n; i++) {
        uint32_t a, b, c;
        if (s->selection == DE_SELECT_SAMPLE) {
            de_sample3(&r, n, i, &a, &b, &c);
        } else {
            uint32_t k = 0;
            for (uint32_t j = 0; j < n; j++)
                if (j != i) { s->keys[k] = de_rng_u64(&r); s->idx[k++] = j; }
            de_sort(s->keys, s->idx, k, s->tkeys, s->tidx);
            a = s->idx[0]; b = s->idx[1]; c = s->idx[2];
        }
        const uint32_t R = de_rng_below(&r, (uint32_t)d);
        const double *restrict xi = x + (size_t)i * d;
        const double *restrict xa = x + (size_t)a * d;
        const double *restrict xb = x + (size_t)b * d;
        const double *restrict xc = x + (size_t)c * d;
        double *restrict t = x + (size_t)(n + i) * d;
        for (int j = 0; j < d; j++) {
            const double mutant = xa[j] + F * (xb[j] - xc[j]);
            t[j] = (de_rng_u64(&r) < thr || (uint32_t)j == R) ? mutant : xi[j];
        }
    }
    s->rng = r;

    /* 2. Evaluate feasible trials and build the survivor pool (C# order:
     *    offspring first if it survives, then the surviving parent). */
    uint32_t cnt = 0;
    for (uint32_t i = 0; i < n; i++) {
        const uint32_t o = n + i;
        const double *xo = x + (size_t)o * d;
        if (p->feasible && !p->feasible(xo, d, p->ctx)) { s->pool[cnt++] = i; continue; }
        double *fo = fit + (size_t)o * m;
        const double *fi = fit + (size_t)i * m;
        p->eval(xo, fo, d, p->ctx);
        if (de_dominates(fo, fi, m)) {
            s->pool[cnt++] = o;                           /* parent eliminated */
        } else {
            if (!de_dominates(fi, fo, m)) s->pool[cnt++] = o; /* mutually non-dominated */
            s->pool[cnt++] = i;
        }
    }

    /* 3. Rank and truncate to n survivors (indices into pool -> selected). */
    uint32_t nsel = 0;
    if (m == 1) {
        for (uint32_t k = 0; k < cnt; k++) { s->keys[k] = de_key(fit[s->pool[k]]); s->idx[k] = k; }
        de_sort(s->keys, s->idx, cnt, s->tkeys, s->tidx);
        nsel = cnt < n ? cnt : n;
        uint32_t rk = 0;
        for (uint32_t k = 0; k < nsel; k++) {
            if (k > 0 && fit[s->pool[s->idx[k - 1]]] < fit[s->pool[s->idx[k]]]) rk++;
            s->selected[k] = s->idx[k];
            s->pool_rank[s->idx[k]] = rk;
        }
    } else {
        de_rank_fronts(fit, m, s->pool, cnt, s->pool_rank, s->keys, s->idx, s->tkeys, s->tidx,
                       s->front_tail, s->prev);
        if (cnt <= n) {
            for (uint32_t k = 0; k < cnt; k++) s->selected[k] = k;
            nsel = cnt;
        } else {
            /* Group by rank, keeping pool order inside a group (counting sort). */
            uint32_t maxr = 0;
            for (uint32_t k = 0; k < cnt; k++) if (s->pool_rank[k] > maxr) maxr = s->pool_rank[k];
            memset(s->counts, 0, (maxr + 2) * sizeof(uint32_t));
            for (uint32_t k = 0; k < cnt; k++) s->counts[s->pool_rank[k] + 1]++;
            for (uint32_t q = 1; q <= maxr + 1; q++) s->counts[q] += s->counts[q - 1];
            for (uint32_t k = 0; k < cnt; k++) s->order[s->counts[s->pool_rank[k]]++] = k;
            /* counts[q] is now the end of group q; group q starts at end of q-1. */
            uint32_t start = 0;
            for (uint32_t q = 0; q <= maxr && nsel < n; q++) {
                const uint32_t end = s->counts[q], len = end - start;
                if (nsel + len <= n) {
                    memcpy(s->selected + nsel, s->order + start, len * sizeof(uint32_t));
                    nsel += len;
                } else {
                    const uint32_t remaining = n - nsel;
                    uint32_t *group = s->order + start;
                    de_crowding(fit, m, s->pool, group, len, s->dist, s->keys, s->idx, s->tkeys, s->tidx);
                    for (uint32_t k = 0; k < len; k++) { s->keys[k] = ~de_key(s->dist[k]); s->idx[k] = k; }
                    de_sort(s->keys, s->idx, len, s->tkeys, s->tidx); /* stable, descending distance */
                    for (uint32_t k = 0; k < remaining; k++) s->selected[nsel++] = group[s->idx[k]];
                    break;
                }
                start = end;
            }
        }
    }

    /* 4. Copy survivors into the other buffer and swap. */
    for (uint32_t k = 0; k < nsel; k++) {
        const uint32_t slot = s->selected[k], row = s->pool[slot];
        memcpy(s->nx + (size_t)k * d, x + (size_t)row * d, (size_t)d * sizeof(double));
        memcpy(s->nfit + (size_t)k * m, fit + (size_t)row * m, (size_t)m * sizeof(double));
        s->nrank[k] = s->pool_rank[slot];
    }
    double *tx = s->x; s->x = s->nx; s->nx = tx;
    double *tf = s->fit; s->fit = s->nfit; s->nfit = tf;
    uint32_t *tr = s->rank; s->rank = s->nrank; s->nrank = tr;
    s->generation_count++;
}

/* Runs the optimisation. `cb` (optional) is called after every generation,
 * like the C# OnGenerationFinished event; a non-zero return stops early. */
DE_INLINE void de_run(de_state *s, const de_problem *p, de_callback cb, void *user)
{
    de_initialize(s, p);
    while (s->generation_count < s->max_generations) {
        de_step(s, p);
        if (cb && cb(s, user)) break;
    }
}

/* Writes the indices of the best individuals into out (capacity n) and
 * returns their count: the single best (first on ties) for one objective,
 * otherwise every individual with the lowest Pareto rank. */
static inline int de_best(const de_state *s, uint32_t *out)
{
    if (s->m == 1) {
        uint32_t best = 0;
        for (int i = 1; i < s->n; i++)
            if (de_key(s->fit[i]) < de_key(s->fit[best])) best = (uint32_t)i;
        out[0] = best;
        return 1;
    }
    uint32_t lowest = UINT32_MAX;
    for (int i = 0; i < s->n; i++) if (s->rank[i] < lowest) lowest = s->rank[i];
    int c = 0;
    for (int i = 0; i < s->n; i++) if (s->rank[i] == lowest) out[c++] = (uint32_t)i;
    return c;
}

#endif /* DE_H */
