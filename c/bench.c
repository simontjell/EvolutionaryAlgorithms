/* Benchmark harness. Mirrors rust/src/bin/bench.rs and the C# harness:
 * same scenarios, same CLI, same JSON-lines output.
 *
 *   bench --list
 *   bench --scenario <name> [--warmup N] [--runs N] [--selection sample|shuffle]
 */
#define _POSIX_C_SOURCE 199309L
#include <stdio.h>
#include <time.h>
#include "de.h"
#include "problems.h"

typedef struct { const char *name; int population, generations; } scenario;

static const scenario SCENARIOS[] = {
    {"sphere_d2_p100_g100", 100, 100},
    {"booth_p100_g100", 100, 100},
    {"rosenbrock_d10_p100_g500", 100, 500},
    {"sphere_d30_p500_g200", 500, 200},
    {"schaffer_p100_g100", 100, 100},
    {"schaffer_p500_g100", 500, 100},
};
#define N_SCENARIOS (sizeof SCENARIOS / sizeof *SCENARIOS)

static int dim_of(const char *name)
{
    const char *p = strstr(name, "_d");
    return p ? atoi(p + 2) : 0;
}

/* One specialised, fully inlined optimiser per problem (eval/feasible are
 * constant-propagated into the generation loop). */
#define DEFINE_RUN(fn, PROBLEM_EXPR)                                         \
    static double fn(const scenario *sc, uint64_t seed, de_selection sel)   \
    {                                                                        \
        const de_problem p = PROBLEM_EXPR;                                   \
        de_params prm = {sc->population, DE_DEFAULT_CR, DE_DEFAULT_F,        \
                         sc->generations, sel, seed, NULL, 0};               \
        de_state s;                                                          \
        if (de_init(&s, &p, &prm)) { fputs("init failed\n", stderr); exit(1); } \
        de_run(&s, &p, NULL, NULL);                                          \
        uint32_t *b = malloc(sizeof(uint32_t) * (size_t)sc->population);     \
        int c = de_best(&s, b);                                              \
        double r = s.m == 1 ? s.fit[b[0]] : (double)c;                       \
        free(b);                                                             \
        de_free(&s);                                                         \
        return r;                                                            \
    }

DEFINE_RUN(run_sphere, problem_sphere(dim_of(sc->name)))
DEFINE_RUN(run_booth, problem_booth())
DEFINE_RUN(run_rosenbrock, problem_rosenbrock(dim_of(sc->name)))
DEFINE_RUN(run_schaffer, problem_schaffer())

static double run(const scenario *sc, uint64_t seed, de_selection sel)
{
    if (!strncmp(sc->name, "sphere", 6)) return run_sphere(sc, seed, sel);
    if (!strncmp(sc->name, "booth", 5)) return run_booth(sc, seed, sel);
    if (!strncmp(sc->name, "rosenbrock", 10)) return run_rosenbrock(sc, seed, sel);
    if (!strncmp(sc->name, "schaffer", 8)) return run_schaffer(sc, seed, sel);
    fprintf(stderr, "unknown problem in %s\n", sc->name);
    exit(1);
}

static double now_ms(void)
{
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return ts.tv_sec * 1e3 + ts.tv_nsec / 1e6;
}

int main(int argc, char **argv)
{
    const char *name = NULL;
    int warmup = 2, runs = 10;
    de_selection sel = DE_SELECT_SAMPLE;
    for (int i = 1; i < argc; i++) {
        if (!strcmp(argv[i], "--list")) {
            for (size_t k = 0; k < N_SCENARIOS; k++) puts(SCENARIOS[k].name);
            return 0;
        }
        if (i + 1 >= argc) break;
        if (!strcmp(argv[i], "--scenario")) name = argv[++i];
        else if (!strcmp(argv[i], "--warmup")) warmup = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--runs")) runs = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--selection")) sel = !strcmp(argv[++i], "shuffle") ? DE_SELECT_SHUFFLE : DE_SELECT_SAMPLE;
    }
    if (!name) { fputs("--scenario <name> required\n", stderr); return 2; }
    const scenario *sc = NULL;
    for (size_t k = 0; k < N_SCENARIOS; k++) if (!strcmp(SCENARIOS[k].name, name)) sc = &SCENARIOS[k];
    if (!sc) { fprintf(stderr, "unknown scenario %s\n", name); return 2; }

    const char *lang = sel == DE_SELECT_SHUFFLE ? "c_shuffle" : "c";
    volatile double sink = 0;
    for (int k = 0; k < warmup; k++) sink += run(sc, 1000 + (uint64_t)k, sel);
    for (int k = 0; k < runs; k++) {
        double t0 = now_ms();
        double best = run(sc, (uint64_t)k, sel);
        double ms = now_ms() - t0;
        sink += best;
        printf("{\"lang\":\"%s\",\"scenario\":\"%s\",\"run\":%d,\"ms\":%.4f,\"best\":%.6e}\n", lang, sc->name, k, ms, best);
    }
    (void)sink;
    return 0;
}
