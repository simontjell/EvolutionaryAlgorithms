/* Benchmark problems, matching tests/DifferentialEvolution.Tests/OptimizationProblems. */
#ifndef DE_PROBLEMS_H
#define DE_PROBLEMS_H

#include "de.h"

static void sphere_eval(const double *x, double *f, int d, const void *ctx)
{
    (void)ctx;
    double s = 0.0;
    for (int i = 0; i < d; i++) s += x[i] * x[i];
    f[0] = s;
}

static void booth_eval(const double *x, double *f, int d, const void *ctx)
{
    (void)d; (void)ctx;
    const double a = x[0] + 2.0 * x[1] - 7.0, b = 2.0 * x[0] + x[1] - 5.0;
    f[0] = a * a + b * b;
}

static bool booth_feasible(const double *x, int d, const void *ctx)
{
    (void)d; (void)ctx;
    return x[0] >= -1.0 && x[1] <= 10.0;
}

static void rosenbrock_eval(const double *x, double *f, int d, const void *ctx)
{
    (void)ctx;
    double s = 0.0;
    for (int i = 0; i < d - 1; i++) {
        const double a = x[i + 1] - x[i] * x[i], b = 1.0 - x[i];
        s += 100.0 * a * a + b * b;
    }
    f[0] = s;
}

/* Schaffer function N. 1; ctx points to the bound `a` (>= 10). */
static void schaffer_eval(const double *x, double *f, int d, const void *ctx)
{
    (void)d; (void)ctx;
    const double b = x[0] - 2.0;
    f[0] = x[0] * x[0];
    f[1] = b * b;
}

static bool schaffer_feasible(const double *x, int d, const void *ctx)
{
    (void)d;
    const double a = *(const double *)ctx;
    return x[0] >= -a && x[0] <= a;
}

static void schaffer_init(double *x, int d, de_rng *rng, const void *ctx)
{
    (void)d;
    const double a = *(const double *)ctx;
    x[0] = -a + de_rng_f64(rng) * 2.0 * a;
    x[1] = -a + de_rng_f64(rng) * 2.0 * a;
}

static const double SCHAFFER_A = 10.0;

static inline de_problem problem_sphere(int d) { return (de_problem){d, 1, sphere_eval, NULL, NULL, NULL}; }
static inline de_problem problem_booth(void) { return (de_problem){2, 1, booth_eval, booth_feasible, NULL, NULL}; }
static inline de_problem problem_rosenbrock(int d) { return (de_problem){d, 1, rosenbrock_eval, NULL, NULL, NULL}; }
static inline de_problem problem_schaffer(void)
{
    return (de_problem){2, 2, schaffer_eval, schaffer_feasible, schaffer_init, &SCHAFFER_A};
}

#endif
