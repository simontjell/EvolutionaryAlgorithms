/*
 * tui.c - terminal UI for watching the C Differential Evolution run.
 *
 * Shows the population (genes for single-objective problems, objective space
 * for Schaffer) as a braille scatter plot, the best fitness (or Pareto front
 * size) per generation, and a parameter panel that can be changed while the
 * optimisation runs. Plain ANSI escape codes + termios; no dependencies.
 *
 *   make tui && ./tui
 *
 * Keys: space pause/resume   s step one generation   r restart   n new seed
 *       up/down (k/j) select parameter   left/right (h/l) change value
 *       +/- speed   z auto-zoom on/off   q quit
 */
#define _DEFAULT_SOURCE
#include <errno.h>
#include <math.h>
#include <signal.h>
#include <stdarg.h>
#include <stdio.h>
#include <sys/ioctl.h>
#include <sys/select.h>
#include <termios.h>
#include <time.h>
#include <unistd.h>
#include "de.h"
#include "problems.h"

/* ------------------------------------------------------- extra problems */

static void rastrigin_eval(const double *x, double *f, int d, const void *ctx)
{
    (void)ctx;
    double s = 10.0 * d;
    for (int i = 0; i < d; i++) s += x[i] * x[i] - 10.0 * cos(2.0 * M_PI * x[i]);
    f[0] = s;
}

static void rastrigin_init(double *x, int d, de_rng *rng, const void *ctx)
{
    (void)ctx;
    for (int i = 0; i < d; i++) x[i] = -5.12 + 10.24 * de_rng_f64(rng);
}

static void himmelblau_eval(const double *x, double *f, int d, const void *ctx)
{
    (void)d; (void)ctx;
    const double a = x[0] * x[0] + x[1] - 11.0, b = x[0] + x[1] * x[1] - 7.0;
    f[0] = a * a + b * b;
}

static void himmelblau_init(double *x, int d, de_rng *rng, const void *ctx)
{
    (void)ctx;
    for (int i = 0; i < d; i++) x[i] = -5.0 + 10.0 * de_rng_f64(rng);
}

typedef struct { double x, y; } point;

typedef struct {
    const char *name, *formula;
    int objectives;
    bool dims_adjustable;
    de_eval_fn eval;
    de_feasible_fn feasible;
    de_init_fn init;
    const void *ctx;
    double vx0, vx1, vy0, vy1; /* fixed view (genes, or objectives for multi-objective) */
    int n_optima;
    point optima[4];
} problem_def;

static const problem_def PROBLEMS[] = {
    {"Sphere", "sum x_i^2", 1, true, sphere_eval, NULL, NULL, NULL, -0.5, 1.5, -0.5, 1.5, 1, {{0, 0}}},
    {"Booth", "(x+2y-7)^2 + (2x+y-5)^2", 1, false, booth_eval, booth_feasible, NULL, NULL, -1, 4, -1, 4, 1, {{1, 3}}},
    {"Rosenbrock", "sum 100(x_i+1 - x_i^2)^2 + (1-x_i)^2", 1, true, rosenbrock_eval, NULL, NULL, NULL,
     -0.5, 1.5, -0.5, 1.5, 1, {{1, 1}}},
    {"Rastrigin", "10d + sum x_i^2 - 10cos(2 pi x_i)", 1, true, rastrigin_eval, NULL, rastrigin_init, NULL,
     -5.12, 5.12, -5.12, 5.12, 1, {{0, 0}}},
    {"Himmelblau", "(x^2+y-11)^2 + (x+y^2-7)^2", 1, false, himmelblau_eval, NULL, himmelblau_init, NULL,
     -5, 5, -5, 5, 4, {{3, 2}, {-2.805118, 3.131312}, {-3.779310, -3.283186}, {3.584428, -1.848126}}},
    {"Schaffer N.1", "f1 = x^2, f2 = (x-2)^2", 2, false, schaffer_eval, schaffer_feasible, schaffer_init,
     &SCHAFFER_A, 0, 4.5, 0, 4.5, 0, {{0, 0}}},
};
#define N_PROBLEMS ((int)(sizeof PROBLEMS / sizeof *PROBLEMS))

static const int POPULATIONS[] = {4, 6, 10, 20, 30, 50, 100, 200, 300, 500, 1000, 2000};
static const int GENERATIONS[] = {20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 50000};
static const int SPEEDS[] = {1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 5000, 0 /* max */};
#define LEN(a) ((int)(sizeof a / sizeof *a))

/* ------------------------------------------------------------ app state */

enum { P_PROBLEM, P_DIMS, P_POP, P_CR, P_F, P_GENS, P_SELECTION, P_SEED, P_SPEED, P_COUNT };

typedef struct {
    int problem, dims, pop_idx, gens_idx, speed_idx;
    double cr, f;
    de_selection selection;
    uint64_t seed;
    bool paused, zoom, done;
    int selected;

    de_problem p;
    de_state s;
    bool have_state;
    uint32_t *best_idx;
    double *history;      /* best fitness (or front size) per generation */
    int history_cap;
    double step_us;       /* EMA of de_step() time */
    double acc;           /* fractional generations owed to the speed setting */
    double rate;          /* measured generations per second */
    int rate_gens;
    double rate_t0;
    char message[96];
} app;

static double now_s(void)
{
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return ts.tv_sec + ts.tv_nsec * 1e-9;
}

static int app_population(const app *a) { return POPULATIONS[a->pop_idx]; }
static int app_max_gens(const app *a) { return GENERATIONS[a->gens_idx]; }

static void record_history(app *a)
{
    const de_state *s = &a->s;
    int g = s->generation_count - 1;
    if (g >= a->history_cap) {
        int cap = a->history_cap ? a->history_cap * 2 : 1024;
        while (cap <= g) cap *= 2;
        a->history = realloc(a->history, sizeof(double) * cap);
        a->history_cap = cap;
    }
    int c = de_best(s, a->best_idx);
    a->history[g] = s->m == 1 ? s->fit[a->best_idx[0]] : (double)c;
}

static void app_restart(app *a)
{
    if (a->have_state) de_free(&a->s);
    const problem_def *pd = &PROBLEMS[a->problem];
    int dims = pd->dims_adjustable ? a->dims : 2;
    a->p = (de_problem){dims, pd->objectives, pd->eval, pd->feasible, pd->init, pd->ctx};
    de_params prm = {app_population(a), a->cr, a->f, app_max_gens(a), a->selection, a->seed, NULL, 0};
    if (de_init(&a->s, &a->p, &prm)) {
        fprintf(stderr, "de_init failed\n");
        exit(1);
    }
    a->have_state = true;
    free(a->best_idx);
    a->best_idx = malloc(sizeof(uint32_t) * app_population(a));
    de_initialize(&a->s, &a->p);
    record_history(a);
    a->done = false;
    a->acc = 0;
    a->step_us = 0;
    a->rate = 0;
    a->rate_gens = 0;
    a->rate_t0 = now_s();
}

static void app_step(app *a)
{
    if (a->s.generation_count >= app_max_gens(a)) {
        a->done = true;
        return;
    }
    double t0 = now_s();
    de_step(&a->s, &a->p);
    double us = (now_s() - t0) * 1e6;
    a->step_us = a->step_us == 0 ? us : 0.9 * a->step_us + 0.1 * us;
    record_history(a);
    a->rate_gens++;
    if (a->s.generation_count >= app_max_gens(a)) a->done = true;
}

/* ------------------------------------------------------------- terminal */

static struct termios orig_termios;
static volatile sig_atomic_t quit_requested = 0;

static void term_restore(void)
{
    tcsetattr(STDIN_FILENO, TCSAFLUSH, &orig_termios);
    const char *seq = "\x1b[0m\x1b[?25h\x1b[?1049l";
    if (write(STDOUT_FILENO, seq, strlen(seq)) < 0) { /* nothing to do */ }
}

static void on_signal(int sig) { (void)sig; quit_requested = 1; }

static void term_setup(void)
{
    tcgetattr(STDIN_FILENO, &orig_termios);
    atexit(term_restore);
    struct termios raw = orig_termios;
    raw.c_lflag &= ~(ECHO | ICANON);
    raw.c_cc[VMIN] = 0;
    raw.c_cc[VTIME] = 0;
    tcsetattr(STDIN_FILENO, TCSANOW, &raw);
    signal(SIGINT, on_signal);
    signal(SIGTERM, on_signal);
    const char *seq = "\x1b[?1049h\x1b[?25l\x1b[2J";
    if (write(STDOUT_FILENO, seq, strlen(seq)) < 0) exit(1);
}

static void term_size(int *w, int *h)
{
    struct winsize ws;
    if (ioctl(STDOUT_FILENO, TIOCGWINSZ, &ws) == 0 && ws.ws_col > 0) { *w = ws.ws_col; *h = ws.ws_row; }
    else { *w = 100; *h = 32; }
}

enum { K_NONE, K_UP, K_DOWN, K_LEFT, K_RIGHT };

/* Returns a character, or K_UP.. for arrow keys (as negative values), or 0. */
static int read_key(void)
{
    unsigned char c;
    if (read(STDIN_FILENO, &c, 1) != 1) return 0;
    if (c != 0x1b) return c;
    unsigned char seq[2];
    if (read(STDIN_FILENO, &seq[0], 1) != 1) return 0x1b;
    if (read(STDIN_FILENO, &seq[1], 1) != 1) return 0x1b;
    if (seq[0] == '[' || seq[0] == 'O') {
        switch (seq[1]) {
        case 'A': return -K_UP;
        case 'B': return -K_DOWN;
        case 'C': return -K_RIGHT;
        case 'D': return -K_LEFT;
        }
    }
    return 0;
}

/* --------------------------------------------------------------- screen */

typedef struct {
    char ch[5];
    uint8_t sgr;  /* foreground SGR code, 0 = default */
    uint8_t attr; /* 1 = bold, 2 = reverse, 4 = dim */
} cell;

typedef struct {
    int w, h;
    cell *cells;
    char *out;
    size_t out_cap;
} screen;

enum { C_DEFAULT = 0, C_RED = 31, C_GREEN = 32, C_YELLOW = 33, C_BLUE = 34, C_MAGENTA = 35, C_CYAN = 36,
       C_GRAY = 90, C_BRED = 91, C_BGREEN = 92, C_BYELLOW = 93, C_BCYAN = 96, C_WHITE = 97 };

static void scr_resize(screen *sc, int w, int h)
{
    if (sc->w == w && sc->h == h) return;
    sc->w = w; sc->h = h;
    sc->cells = realloc(sc->cells, sizeof(cell) * w * h);
    sc->out_cap = (size_t)w * h * 16 + 1024;
    sc->out = realloc(sc->out, sc->out_cap);
}

static void scr_clear(screen *sc)
{
    for (int i = 0; i < sc->w * sc->h; i++) sc->cells[i] = (cell){" ", 0, 0};
}

static int utf8_len(unsigned char c) { return c < 0x80 ? 1 : c < 0xE0 ? 2 : c < 0xF0 ? 3 : 4; }

/* Writes a UTF-8 string starting at (x, y), one code point per cell; returns columns written. */
static int scr_put(screen *sc, int x, int y, const char *s, uint8_t sgr, uint8_t attr)
{
    int col = 0;
    if (y < 0 || y >= sc->h) return 0;
    while (*s) {
        int n = utf8_len((unsigned char)*s);
        if (x + col >= 0 && x + col < sc->w) {
            cell *c = &sc->cells[y * sc->w + x + col];
            memcpy(c->ch, s, n);
            c->ch[n] = 0;
            c->sgr = sgr;
            c->attr = attr;
        }
        s += n;
        col++;
    }
    return col;
}

static int scr_printf(screen *sc, int x, int y, uint8_t sgr, uint8_t attr, const char *fmt, ...)
    __attribute__((format(printf, 6, 7)));

static int scr_printf(screen *sc, int x, int y, uint8_t sgr, uint8_t attr, const char *fmt, ...)
{
    char buf[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(buf, sizeof buf, fmt, ap);
    va_end(ap);
    return scr_put(sc, x, y, buf, sgr, attr);
}

/* Writes " text " but never more than max_cols columns (cuts at a code point boundary, adds "…"). */
static void scr_put_clipped(screen *sc, int x, int y, int max_cols, const char *text, uint8_t sgr, uint8_t attr)
{
    char buf[512];
    int cols = 0;
    size_t o = 0, i = 0;
    buf[o++] = ' ';
    while (text[i] && o + 8 < sizeof buf) {
        int n = utf8_len((unsigned char)text[i]);
        if (cols + 3 > max_cols) { memcpy(buf + o, "…", 3); o += 3; cols++; break; }
        memcpy(buf + o, text + i, n);
        o += n; i += n; cols++;
    }
    buf[o++] = ' ';
    buf[o] = 0;
    scr_put(sc, x, y, buf, sgr, attr);
}

static void scr_box(screen *sc, int x, int y, int w, int h, const char *title, const char *footer)
{
    for (int i = 1; i < w - 1; i++) {
        scr_put(sc, x + i, y, "─", C_GRAY, 0);
        scr_put(sc, x + i, y + h - 1, "─", C_GRAY, 0);
    }
    for (int j = 1; j < h - 1; j++) {
        scr_put(sc, x, y + j, "│", C_GRAY, 0);
        scr_put(sc, x + w - 1, y + j, "│", C_GRAY, 0);
    }
    scr_put(sc, x, y, "┌", C_GRAY, 0);
    scr_put(sc, x + w - 1, y, "┐", C_GRAY, 0);
    scr_put(sc, x, y + h - 1, "└", C_GRAY, 0);
    scr_put(sc, x + w - 1, y + h - 1, "┘", C_GRAY, 0);
    if (title) scr_put_clipped(sc, x + 2, y, w - 4, title, C_WHITE, 1);
    if (footer) scr_put_clipped(sc, x + 2, y + h - 1, w - 4, footer, C_GRAY, 0);
}

static void scr_flush(screen *sc)
{
    char *o = sc->out;
    o += sprintf(o, "\x1b[H");
    int cur_sgr = -1, cur_attr = -1;
    for (int y = 0; y < sc->h; y++) {
        if (y) { *o++ = '\r'; *o++ = '\n'; }
        for (int x = 0; x < sc->w; x++) {
            const cell *c = &sc->cells[y * sc->w + x];
            if (c->sgr != cur_sgr || c->attr != cur_attr) {
                o += sprintf(o, "\x1b[0%s%s%s", c->attr & 1 ? ";1" : "", c->attr & 2 ? ";7" : "",
                             c->attr & 4 ? ";2" : "");
                if (c->sgr) o += sprintf(o, ";%d", c->sgr);
                *o++ = 'm';
                cur_sgr = c->sgr;
                cur_attr = c->attr;
            }
            size_t n = strlen(c->ch);
            memcpy(o, c->ch, n);
            o += n;
        }
    }
    o += sprintf(o, "\x1b[0m");
    size_t len = (size_t)(o - sc->out), off = 0;
    while (off < len) {
        ssize_t r = write(STDOUT_FILENO, sc->out + off, len - off);
        if (r < 0) { if (errno == EINTR) continue; break; }
        off += (size_t)r;
    }
}

/* -------------------------------------------------------- braille canvas */

typedef struct {
    int x, y, w, h; /* screen cells */
    uint8_t *bits;
    uint8_t *sgr;
    uint8_t *prio;
} canvas;

static void cv_init(canvas *cv, int x, int y, int w, int h)
{
    cv->x = x; cv->y = y; cv->w = w; cv->h = h;
    cv->bits = calloc((size_t)w * h, 1);
    cv->sgr = calloc((size_t)w * h, 1);
    cv->prio = calloc((size_t)w * h, 1);
}

static void cv_free(canvas *cv) { free(cv->bits); free(cv->sgr); free(cv->prio); }

/* Dot at pixel (px, py), py = 0 at the top. */
static void cv_dot(canvas *cv, int px, int py, uint8_t sgr, uint8_t prio)
{
    if (px < 0 || py < 0 || px >= cv->w * 2 || py >= cv->h * 4) return;
    static const uint8_t BIT[4][2] = {{0x01, 0x08}, {0x02, 0x10}, {0x04, 0x20}, {0x40, 0x80}};
    int i = (py / 4) * cv->w + px / 2;
    cv->bits[i] |= BIT[py % 4][px % 2];
    if (prio >= cv->prio[i]) { cv->prio[i] = prio; cv->sgr[i] = sgr; }
}

typedef struct { double x0, x1, y0, y1; } view;

static bool cv_map(const canvas *cv, const view *v, double x, double y, int *px, int *py)
{
    if (!(x >= v->x0 && x <= v->x1 && y >= v->y0 && y <= v->y1)) return false;
    *px = (int)lround((x - v->x0) / (v->x1 - v->x0) * (cv->w * 2 - 1));
    *py = (int)lround((v->y1 - y) / (v->y1 - v->y0) * (cv->h * 4 - 1));
    return true;
}

static void cv_point(canvas *cv, const view *v, double x, double y, uint8_t sgr, uint8_t prio)
{
    int px, py;
    if (cv_map(cv, v, x, y, &px, &py)) cv_dot(cv, px, py, sgr, prio);
}

static void cv_blit(const canvas *cv, screen *sc)
{
    for (int j = 0; j < cv->h; j++)
        for (int i = 0; i < cv->w; i++) {
            uint8_t b = cv->bits[j * cv->w + i];
            if (!b) continue;
            char ch[4] = {(char)0xE2, (char)(0xA0 | (b >> 6)), (char)(0x80 | (b & 0x3F)), 0};
            scr_put(sc, cv->x + i, cv->y + j, ch, cv->sgr[j * cv->w + i], 0);
        }
}

/* Marker character overlaid at a data position (e.g. a known optimum). */
static void cv_marker(const canvas *cv, screen *sc, const view *v, double x, double y, const char *m, uint8_t sgr)
{
    int px, py;
    if (!cv_map(cv, v, x, y, &px, &py)) return;
    if (cv->bits[(py / 4) * cv->w + px / 2]) return; /* don't hide individuals sitting on the optimum */
    scr_put(sc, cv->x + px / 2, cv->y + py / 4, m, sgr, 1);
}

/* ---------------------------------------------------------------- views */

static void plot_xy(const app *a, int i, double *x, double *y)
{
    const de_state *s = &a->s;
    if (s->m >= 2) { *x = s->fit[(size_t)i * s->m]; *y = s->fit[(size_t)i * s->m + 1]; }
    else { *x = s->x[(size_t)i * s->d]; *y = s->x[(size_t)i * s->d + 1]; }
}

static view compute_view(const app *a)
{
    const problem_def *pd = &PROBLEMS[a->problem];
    view v = {pd->vx0, pd->vx1, pd->vy0, pd->vy1};
    if (!a->zoom) return v;
    double x0 = INFINITY, x1 = -INFINITY, y0 = INFINITY, y1 = -INFINITY;
    for (int i = 0; i < a->s.n; i++) {
        double x, y;
        plot_xy(a, i, &x, &y);
        if (!isfinite(x) || !isfinite(y)) continue;
        x0 = fmin(x0, x); x1 = fmax(x1, x); y0 = fmin(y0, y); y1 = fmax(y1, y);
    }
    if (a->s.m >= 2) { /* include the true Pareto front: f1, f2 in [0, 4] */
        x0 = fmin(x0, 0); x1 = fmax(x1, 4); y0 = fmin(y0, 0); y1 = fmax(y1, 4);
    }
    for (int k = 0; k < pd->n_optima; k++) {
        x0 = fmin(x0, pd->optima[k].x); x1 = fmax(x1, pd->optima[k].x);
        y0 = fmin(y0, pd->optima[k].y); y1 = fmax(y1, pd->optima[k].y);
    }
    if (!isfinite(x0)) return v;
    double sx = fmax(x1 - x0, 1e-12 * fmax(1.0, fabs(x0))), sy = fmax(y1 - y0, 1e-12 * fmax(1.0, fabs(y0)));
    return (view){x0 - 0.08 * sx, x1 + 0.08 * sx, y0 - 0.08 * sy, y1 + 0.08 * sy};
}

static void draw_population(const app *a, screen *sc, int x, int y, int w, int h)
{
    const problem_def *pd = &PROBLEMS[a->problem];
    const de_state *s = &a->s;
    view v = compute_view(a);
    char title[128], footer[160];
    if (s->m >= 2) {
        snprintf(title, sizeof title, "Objective space  (f1, f2)  %s", pd->formula);
        snprintf(footer, sizeof footer, "f1 [%.3g, %.3g]  f2 [%.3g, %.3g]   green = Pareto front 0", v.x0, v.x1, v.y0, v.y1);
    } else {
        snprintf(title, sizeof title, "Population  (x0, x1%s)  f = %s", s->d > 2 ? ", projection" : "", pd->formula);
        snprintf(footer, sizeof footer, "x0 [%.4g, %.4g]  x1 [%.4g, %.4g]   yellow = best  red + = optimum",
                 v.x0, v.x1, v.y0, v.y1);
    }
    scr_box(sc, x, y, w, h, title, footer);

    canvas cv;
    cv_init(&cv, x + 1, y + 1, w - 2, h - 2);
    if (s->m >= 2) { /* true Pareto front: x in [0, 2] */
        for (int k = 0; k <= 400; k++) {
            double t = 2.0 * k / 400;
            cv_point(&cv, &v, t * t, (t - 2) * (t - 2), C_GRAY, 1);
        }
    }
    uint32_t best = a->best_idx[0];
    for (int i = 0; i < s->n; i++) {
        double px, py;
        plot_xy(a, i, &px, &py);
        uint8_t col, prio;
        if (s->m >= 2) { col = s->rank[i] == 0 ? C_BGREEN : C_CYAN; prio = s->rank[i] == 0 ? 3 : 2; }
        else if ((uint32_t)i == best) { col = C_BYELLOW; prio = 4; }
        else { col = C_BCYAN; prio = 2; }
        cv_point(&cv, &v, px, py, col, prio);
    }
    cv_blit(&cv, sc);
    for (int k = 0; k < pd->n_optima; k++)
        cv_marker(&cv, sc, &v, pd->optima[k].x, pd->optima[k].y, "+", C_BRED);
    cv_free(&cv);
}

#define LOG_FLOOR 1e-20 /* fitness values below this (incl. exactly 0) are drawn at the floor */

static void draw_history(const app *a, screen *sc, int x, int y, int w, int h)
{
    const de_state *s = &a->s;
    const bool mo = s->m >= 2;
    const int n = s->generation_count;
    const int label_w = 9;
    double lo = INFINITY, hi = -INFINITY;
    for (int g = 0; g < n; g++) {
        double v = mo ? a->history[g] : log10(fmax(a->history[g], LOG_FLOOR));
        lo = fmin(lo, v); hi = fmax(hi, v);
    }
    if (mo) { lo = 0; hi = fmax(hi, (double)s->n); }
    if (hi - lo < 1e-9) { hi += 0.5; lo -= 0.5; }

    char title[96];
    snprintf(title, sizeof title, mo ? "Size of Pareto front 0 per generation" : "Best fitness per generation (log scale)");
    char footer[64];
    snprintf(footer, sizeof footer, "generation 0 .. %d", app_max_gens(a));
    scr_box(sc, x, y, w, h, title, footer);

    if (mo) {
        scr_printf(sc, x + 1, y + 1, C_GRAY, 0, "%*.0f", label_w - 1, hi);
        scr_printf(sc, x + 1, y + h - 2, C_GRAY, 0, "%*.0f", label_w - 1, lo);
    } else {
        scr_printf(sc, x + 1, y + 1, C_GRAY, 0, "%*.1e", label_w - 1, pow(10, hi));
        if (lo <= log10(LOG_FLOOR))
            scr_printf(sc, x + 1, y + h - 2, C_GRAY, 0, "%*s", label_w - 1, "<=1e-20");
        else
            scr_printf(sc, x + 1, y + h - 2, C_GRAY, 0, "%*.1e", label_w - 1, pow(10, lo));
    }

    canvas cv;
    cv_init(&cv, x + label_w, y + 1, w - label_w - 1, h - 2);
    const int pw = cv.w * 2, ph = cv.h * 4;
    const int max_g = app_max_gens(a);
    int prev_py = -1;
    for (int px = 0; px < pw; px++) {
        /* x axis spans the whole run (0 .. max generations) */
        int g = (int)((double)px / (pw - 1) * (max_g - 1));
        if (g >= n) break;
        double v = mo ? a->history[g] : log10(fmax(a->history[g], LOG_FLOOR));
        int py = (int)lround((hi - v) / (hi - lo) * (ph - 1));
        if (prev_py >= 0) {
            int a0 = prev_py < py ? prev_py : py, a1 = prev_py < py ? py : prev_py;
            for (int q = a0; q <= a1; q++) cv_dot(&cv, px, q, C_BGREEN, 1);
        } else {
            cv_dot(&cv, px, py, C_BGREEN, 1);
        }
        prev_py = py;
    }
    cv_blit(&cv, sc);
    cv_free(&cv);
}

static const char *param_name(int i)
{
    static const char *names[] = {"Problem", "Dimensions", "Population", "CR (crossover)", "F (weight)",
                                  "Generations", "Donor select", "Seed", "Speed"};
    return names[i];
}

static void param_value(const app *a, int i, char *buf, size_t n)
{
    const problem_def *pd = &PROBLEMS[a->problem];
    switch (i) {
    case P_PROBLEM: snprintf(buf, n, "%s", pd->name); break;
    case P_DIMS: pd->dims_adjustable ? snprintf(buf, n, "%d", a->dims) : snprintf(buf, n, "2 (fixed)"); break;
    case P_POP: snprintf(buf, n, "%d", app_population(a)); break;
    case P_CR: snprintf(buf, n, "%.2f", a->cr); break;
    case P_F: snprintf(buf, n, "%.2f", a->f); break;
    case P_GENS: snprintf(buf, n, "%d", app_max_gens(a)); break;
    case P_SELECTION: snprintf(buf, n, "%s", a->selection == DE_SELECT_SAMPLE ? "O(1) sample" : "shuffle (C#)"); break;
    case P_SEED: snprintf(buf, n, "%llu", (unsigned long long)a->seed); break;
    case P_SPEED:
        SPEEDS[a->speed_idx] ? snprintf(buf, n, "%d gen/s", SPEEDS[a->speed_idx]) : snprintf(buf, n, "max");
        break;
    }
}

static void draw_panel(const app *a, screen *sc, int x, int y, int w, int h)
{
    scr_box(sc, x, y, w, h, "Parameters", NULL);
    int row = y + 1;
    for (int i = 0; i < P_COUNT; i++, row++) {
        char val[48];
        param_value(a, i, val, sizeof val);
        bool sel = i == a->selected;
        bool restarts = i == P_PROBLEM || i == P_DIMS || i == P_POP || i == P_SEED;
        for (int c = 1; c < w - 1; c++) scr_put(sc, x + c, row, " ", C_DEFAULT, sel ? 2 : 0);
        scr_printf(sc, x + 2, row, sel ? C_WHITE : C_DEFAULT, sel ? 3 : 0, "%-14s", param_name(i));
        scr_printf(sc, x + 17, row, sel ? C_WHITE : C_BCYAN, sel ? 3 : 0, sel ? "‹ %s ›" : "  %s", val);
        if (restarts) scr_put(sc, x + w - 3, row, "↻", C_GRAY, sel ? 2 : 0);
    }
    scr_put(sc, x + 2, row++, "↻ = change restarts the run", C_GRAY, 0);

    row++;
    for (int i = 1; i < w - 1; i++) scr_put(sc, x + i, row, "─", C_GRAY, 0);
    scr_put(sc, x + 2, row++, " Run ", C_WHITE, 1);

    const de_state *s = &a->s;
    const char *status = a->done ? "done" : a->paused ? "paused" : "running";
    uint8_t status_col = a->done ? C_BCYAN : a->paused ? C_BYELLOW : C_BGREEN;
    scr_printf(sc, x + 2, row, C_DEFAULT, 0, "Status       ");
    scr_put(sc, x + 15, row++, status, status_col, 1);
    scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Generation   %d / %d", s->generation_count, app_max_gens(a));
    if (s->m == 1) {
        uint32_t b = a->best_idx[0];
        scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Best f       %.4e", s->fit[b]);
        scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Best x0      %+.6f", s->x[(size_t)b * s->d]);
        scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Best x1      %+.6f", s->x[(size_t)b * s->d + 1]);
    } else {
        uint32_t fronts = 0;
        for (int i = 0; i < s->n; i++) if (s->rank[i] + 1 > fronts) fronts = s->rank[i] + 1;
        scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Front 0      %d / %d", (int)a->history[s->generation_count - 1], s->n);
        if (s->generation_count > 1)
            scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Fronts       %u", fronts);
        else /* like C#, the initial "rank" is the number of dominators, not a front index */
            scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Fronts       -");
        row++;
    }
    scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Step time    %.1f µs/gen", a->step_us);
    scr_printf(sc, x + 2, row++, C_DEFAULT, 0, "Actual rate  %.0f gen/s", a->rate);
    if (a->message[0]) scr_put(sc, x + 2, row + 1, a->message, C_BYELLOW, 0);
}

static void draw(app *a, screen *sc)
{
    int w, h;
    term_size(&w, &h);
    scr_resize(sc, w, h);
    scr_clear(sc);
    if (w < 70 || h < 22) {
        scr_printf(sc, 0, 0, C_BYELLOW, 1, "Terminal too small (%dx%d); need at least 70x22. q quits.", w, h);
        scr_flush(sc);
        return;
    }
    const int panel_w = 36, left_w = w - panel_w, body_h = h - 1;
    const int top_h = body_h * 3 / 5, bottom_h = body_h - top_h;
    draw_population(a, sc, 0, 0, left_w, top_h);
    draw_history(a, sc, 0, top_h, left_w, bottom_h);
    draw_panel(a, sc, left_w, 0, panel_w, body_h);
    scr_put(sc, 1, h - 1,
            "space pause  s step  r restart  n new seed  ↑↓ select  ←→ change  +/- speed  z zoom  q quit",
            C_GRAY, 0);
    scr_flush(sc);
}

/* ---------------------------------------------------------------- input */

static void change_param(app *a, int dir)
{
    const problem_def *pd;
    a->message[0] = 0;
    switch (a->selected) {
    case P_PROBLEM:
        a->problem = (a->problem + dir + N_PROBLEMS) % N_PROBLEMS;
        app_restart(a);
        break;
    case P_DIMS:
        pd = &PROBLEMS[a->problem];
        if (!pd->dims_adjustable) { snprintf(a->message, sizeof a->message, "%s is 2-dimensional", pd->name); break; }
        a->dims += dir;
        if (a->dims < 2) a->dims = 2;
        if (a->dims > 50) a->dims = 50;
        app_restart(a);
        break;
    case P_POP:
        a->pop_idx += dir;
        if (a->pop_idx < 0) a->pop_idx = 0;
        if (a->pop_idx >= LEN(POPULATIONS)) a->pop_idx = LEN(POPULATIONS) - 1;
        app_restart(a);
        break;
    case P_CR:
        a->cr = fmin(1.0, fmax(0.0, round((a->cr + 0.05 * dir) * 100) / 100));
        de_set_rates(&a->s, a->cr, a->f);
        break;
    case P_F:
        a->f = fmin(2.0, fmax(0.0, round((a->f + 0.05 * dir) * 100) / 100));
        de_set_rates(&a->s, a->cr, a->f);
        break;
    case P_GENS:
        a->gens_idx += dir;
        if (a->gens_idx < 0) a->gens_idx = 0;
        if (a->gens_idx >= LEN(GENERATIONS)) a->gens_idx = LEN(GENERATIONS) - 1;
        a->s.max_generations = app_max_gens(a);
        a->done = a->s.generation_count >= app_max_gens(a);
        break;
    case P_SELECTION:
        a->selection = a->selection == DE_SELECT_SAMPLE ? DE_SELECT_SHUFFLE : DE_SELECT_SAMPLE;
        de_set_selection(&a->s, a->selection);
        a->step_us = 0;
        break;
    case P_SEED:
        if (dir < 0 && a->seed == 0) break;
        a->seed += dir;
        app_restart(a);
        break;
    case P_SPEED:
        a->speed_idx += dir;
        if (a->speed_idx < 0) a->speed_idx = 0;
        if (a->speed_idx >= LEN(SPEEDS)) a->speed_idx = LEN(SPEEDS) - 1;
        break;
    }
}

static bool handle_key(app *a, int k)
{
    switch (k) {
    case 'q': case 'Q': return false;
    case ' ':
        if (a->done) { app_restart(a); a->paused = false; }
        else a->paused = !a->paused;
        break;
    case 's': case '.': a->paused = true; app_step(a); break;
    case 'r': app_restart(a); break;
    case 'n': a->seed++; app_restart(a); break;
    case 'z': a->zoom = !a->zoom; break;
    case '+': case '=': case '-': case '_': {
        int keep = a->selected;
        a->selected = P_SPEED;
        change_param(a, k == '+' || k == '=' ? +1 : -1);
        a->selected = keep;
        break;
    }
    case 'k': case -K_UP: a->selected = (a->selected + P_COUNT - 1) % P_COUNT; break;
    case 'j': case -K_DOWN: a->selected = (a->selected + 1) % P_COUNT; break;
    case 'h': case -K_LEFT: change_param(a, -1); break;
    case 'l': case -K_RIGHT: change_param(a, +1); break;
    }
    return true;
}

/* ----------------------------------------------------------------- main */

int main(void)
{
    if (!isatty(STDIN_FILENO) || !isatty(STDOUT_FILENO)) {
        fprintf(stderr, "tui needs an interactive terminal\n");
        return 1;
    }
    app a = {0};
    a.problem = 0;
    a.dims = 2;
    a.pop_idx = 6;   /* 100 */
    a.gens_idx = 4;  /* 500 */
    a.speed_idx = 4; /* 20 gen/s */
    a.cr = DE_DEFAULT_CR;
    a.f = DE_DEFAULT_F;
    a.selection = DE_SELECT_SAMPLE;
    a.seed = 1;
    a.zoom = true;
    app_restart(&a);

    term_setup();
    screen sc = {0};
    const double frame = 1.0 / 30;
    double last = now_s();
    while (!quit_requested) {
        int k;
        bool keep = true;
        while ((k = read_key()) != 0 && (keep = handle_key(&a, k))) {}
        if (!keep) break;

        double t = now_s(), dt = t - last;
        last = t;
        if (!a.paused && !a.done) {
            int speed = SPEEDS[a.speed_idx];
            if (speed == 0) {
                double end = t + frame * 0.8;
                while (!a.done && now_s() < end) app_step(&a);
            } else {
                /* Run the generations owed by the speed setting, but never more than one
                 * frame's worth of time: if the machine can't keep up, drop the debt
                 * instead of letting it grow (which would freeze the UI). */
                a.acc = fmin(a.acc + speed * dt, speed * frame + 1.0);
                double end = t + frame * 0.8;
                while (a.acc >= 1.0 && !a.done && now_s() < end) { app_step(&a); a.acc -= 1.0; }
                if (a.done || now_s() >= end) a.acc = fmin(a.acc, 1.0);
            }
        }
        double rt = now_s() - a.rate_t0;
        if (rt >= 0.5) { a.rate = a.rate_gens / rt; a.rate_gens = 0; a.rate_t0 = now_s(); }

        draw(&a, &sc);

        double spent = now_s() - t;
        if (spent < frame) {
            fd_set fds;
            FD_ZERO(&fds);
            FD_SET(STDIN_FILENO, &fds);
            struct timeval tv = {0, (suseconds_t)((frame - spent) * 1e6)};
            select(STDIN_FILENO + 1, &fds, NULL, NULL, &tv);
        }
    }
    de_free(&a.s);
    free(a.best_idx);
    free(a.history);
    free(sc.cells);
    free(sc.out);
    return 0;
}
