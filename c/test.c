/* Tests for the C implementation: ports of the C# convergence tests plus
 * checks of the optimised building blocks against simple reference code. */
#include <math.h>
#include <stdio.h>
#include "de.h"
#include "problems.h"

static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL %s:%d: ", __FILE__, __LINE__); \
    printf(__VA_ARGS__); printf("\n"); } } while (0)

/* xUnit Assert.Equal(expected, actual, precision): both rounded to `decimals`. */
static bool close_to(double expected, double actual, int decimals)
{
    double s = pow(10, decimals);
    return round(expected * s) == round(actual * s);
}

static void run_problem(de_state *s, de_problem p, int pop, int gens, de_selection sel, uint64_t seed)
{
    de_params prm = {pop, DE_DEFAULT_CR, DE_DEFAULT_F, gens, sel, seed, NULL, 0};
    if (de_init(s, &p, &prm)) { printf("init failed\n"); exit(1); }
    de_run(s, &p, NULL, NULL);
}

static void test_convergence(de_selection sel)
{
    const char *mode = sel == DE_SELECT_SAMPLE ? "sample" : "shuffle";
    uint32_t best[512];
    de_state s;

    run_problem(&s, problem_booth(), 100, 100, sel, 0);
    CHECK(de_best(&s, best) == 1, "booth best count");
    double *g = s.x + best[0] * s.d;
    CHECK(close_to(1.0, g[0], 2) && close_to(3.0, g[1], 2) && close_to(0.0, s.fit[best[0]], 2),
          "booth (%s): (%g, %g) -> %g", mode, g[0], g[1], s.fit[best[0]]);
    de_free(&s);

    run_problem(&s, problem_sphere(2), 100, 100, sel, 0);
    de_best(&s, best);
    g = s.x + best[0] * s.d;
    CHECK(close_to(0, g[0], 2) && close_to(0, g[1], 2) && close_to(0, s.fit[best[0]], 2),
          "sphere (%s): %g", mode, s.fit[best[0]]);
    CHECK(s.generation_count == 100, "generation count %d", s.generation_count);
    de_free(&s);

    run_problem(&s, problem_rosenbrock(2), 100, 200, sel, 0);
    de_best(&s, best);
    CHECK(s.fit[best[0]] < 1e-4, "rosenbrock (%s): %g", mode, s.fit[best[0]]);
    de_free(&s);

    run_problem(&s, problem_schaffer(), 100, 100, sel, 0);
    int c = de_best(&s, best);
    CHECK(c >= 50, "schaffer (%s): front size %d", mode, c);
    for (int k = 0; k < c; k++) {
        const double *f = s.fit + best[k] * 2;
        CHECK(s.rank[best[k]] == 0, "rank");
        CHECK(!(f[0] > 1.0) || f[1] < 1.0, "schaffer front shape");
        CHECK(!(f[1] > 1.0) || f[0] < 1.0, "schaffer front shape");
    }
    de_free(&s);
}

/* ENS-BS ranking must agree with the peeling reference, incl. duplicates. */
static void test_ranking(void)
{
    enum { MAXN = 400 };
    static double fit[MAXN * 4];
    static uint32_t rows[MAXN], r1[MAXN], r2[MAXN], idx[MAXN], tidx[MAXN], tail[MAXN], prev[MAXN], scratch[MAXN];
    static uint64_t keys[MAXN], tkeys[MAXN];
    de_rng rng;
    de_rng_seed(&rng, 42);
    int mismatches = 0;
    for (int trial = 0; trial < 600; trial++) {
        int m = 2 + trial % 3;
        uint32_t n = 1 + de_rng_below(&rng, MAXN);
        int grid = trial % 2 ? 5 : 1000000; /* coarse grid => many ties/duplicates */
        for (uint32_t i = 0; i < n * (uint32_t)m; i++) fit[i] = (double)de_rng_below(&rng, grid) - grid / 2;
        for (uint32_t i = 0; i < n; i++) rows[i] = n - 1 - i; /* non-trivial row mapping */
        de_rank_fronts(fit, m, rows, n, r1, keys, idx, tkeys, tidx, tail, prev);
        de_rank_peel(fit, m, rows, n, r2, scratch);
        for (uint32_t i = 0; i < n; i++) mismatches += r1[i] != r2[i];
    }
    CHECK(mismatches == 0, "ENS-BS vs reference: %d rank mismatches", mismatches);
}

static void test_sort_stable(void)
{
    uint64_t k[1000], tk[1000];
    uint32_t v[1000], tv[1000];
    de_rng rng;
    de_rng_seed(&rng, 7);
    for (uint32_t i = 0; i < 1000; i++) { k[i] = de_rng_below(&rng, 10); v[i] = i; }
    de_sort(k, v, 1000, tk, tv);
    bool ok = true;
    for (int i = 1; i < 1000; i++) ok &= k[i - 1] < k[i] || (k[i - 1] == k[i] && v[i - 1] < v[i]);
    CHECK(ok, "sort is not stable/ordered");
    CHECK(de_key(-1.0) < de_key(-0.0) && de_key(-0.0) == de_key(0.0) && de_key(0.0) < de_key(1e-300) &&
          de_key(NAN) < de_key(-INFINITY) && de_key(1.0) < de_key(INFINITY), "key order");
}

static void test_crowding(void)
{
    double fit[] = {1, 4, 2, 2, 4, 1};
    uint32_t rows[] = {0, 1, 2}, group[] = {0, 1, 2}, idx[3], tidx[3];
    uint64_t keys[3], tkeys[3];
    double dist[3];
    de_crowding(fit, 2, rows, group, 3, dist, keys, idx, tkeys, tidx);
    CHECK(isinf(dist[0]) && isinf(dist[2]) && fabs(dist[1] - 2.0) < 1e-12, "crowding %g", dist[1]);
}

/* de_sample3 must give distinct indices != i, uniformly. */
static void test_sample3(void)
{
    enum { N = 7, DRAWS = 700000 };
    long hist[N][N][N] = {{{0}}};
    de_rng rng;
    de_rng_seed(&rng, 3);
    bool distinct = true;
    for (int t = 0; t < DRAWS; t++) {
        uint32_t a, b, c;
        de_sample3(&rng, N, 2, &a, &b, &c);
        distinct &= a != b && a != c && b != c && a != 2 && b != 2 && c != 2 && a < N && b < N && c < N;
        hist[a][b][c]++;
    }
    CHECK(distinct, "sample3 produced repeated/excluded indices");
    double expected = DRAWS / (6.0 * 5 * 4), chi2 = 0; /* 120 ordered triples */
    int cells = 0;
    for (int a = 0; a < N; a++) for (int b = 0; b < N; b++) for (int c = 0; c < N; c++)
        if (a != b && a != c && b != c && a != 2 && b != 2 && c != 2) {
            double dlt = hist[a][b][c] - expected;
            chi2 += dlt * dlt / expected;
            cells++;
        }
    CHECK(cells == 120 && chi2 < 180, "sample3 not uniform: chi2=%g over %d cells", chi2, cells); /* df=119 */
}

static int stop_at_7(const de_state *s, void *u) { (*(int *)u)++; return s->generation_count >= 7; }

static void test_callback_and_injection(void)
{
    de_state s;
    de_problem p = problem_sphere(3);
    de_params prm = {20, DE_DEFAULT_CR, DE_DEFAULT_F, 1000, DE_SELECT_SAMPLE, 1, NULL, 0};
    de_init(&s, &p, &prm);
    int calls = 0;
    de_run(&s, &p, stop_at_7, &calls);
    CHECK(s.generation_count == 7 && calls == 6, "callback stop: %d gens, %d calls", s.generation_count, calls);
    de_free(&s);

    double inj[] = {0.0, 0.0};
    de_problem p2 = problem_sphere(2);
    de_params prm2 = {10, DE_DEFAULT_CR, DE_DEFAULT_F, 1, DE_SELECT_SAMPLE, 1, inj, 1};
    de_init(&s, &p2, &prm2);
    de_run(&s, &p2, NULL, NULL);
    uint32_t best[10];
    de_best(&s, best);
    CHECK(s.x[best[0] * 2] == 0.0 && s.x[best[0] * 2 + 1] == 0.0, "injected individual not best");
    de_free(&s);
}

int main(void)
{
    test_sort_stable();
    test_ranking();
    test_crowding();
    test_sample3();
    test_convergence(DE_SELECT_SAMPLE);
    test_convergence(DE_SELECT_SHUFFLE);
    test_callback_and_injection();
    /* Convergence over several seeds (guards against a lucky seed 0). */
    for (uint64_t seed = 1; seed <= 20; seed++) {
        de_state s;
        uint32_t best[1];
        run_problem(&s, problem_sphere(2), 100, 100, DE_SELECT_SAMPLE, seed);
        de_best(&s, best);
        CHECK(s.fit[best[0]] < 1e-8, "sphere seed %llu: %g", (unsigned long long)seed, s.fit[best[0]]);
        de_free(&s);
    }
    if (failures) { printf("%d check(s) failed\n", failures); return 1; }
    printf("all tests passed\n");
    return 0;
}
