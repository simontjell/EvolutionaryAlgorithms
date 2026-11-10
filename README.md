# Introduction

This repository contains both a generic framework for implementing evolutionary algorithms (__src/EvolutionaryAlgorithm__) and an example of a concrete implementation of the Differential Evolution algorithm (__src/DifferentialAlgorithm__) with extensions supporting optimization of multi-objective minimization problems.

# Benchmark problems
The __tests/DifferentialEvolution.Tests/OptimizationProblems__ contains a small collection of benchmark problems that are used for testing the implementation but also serve as good examples of how the framework is applied on an optimization problem.

An optimization problem is specified in code by implementing a subclass of the abstract OptimizationProblem class. The subclass implements three concrete aspects of an optimization problem:

1) Creation of random individuals (__CreateRandomIndividual__). This is only necessary to override if the range of one or more of the genes differs from the default interval ([0...1]).
2) Calculation of fitness values for individuals (__CalculateFitnessValues__) returning a list containing one fitness value per optimization objective.
3) An optional feasibility predicate (__IsFeasible__). If not overridden, all individuals will be considered feasible - i.e., the search space is only limited by the range of the nummeric types used in the algorithm (__double__).

# Building the solution
The current implementation is based on .NET 10. The SDK is found here: [[https://dotnet.microsoft.com/download/dotnet/6.0](https://dotnet.microsoft.com/download/dotnet/6.0)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

After installing the SDK, the code is build using the .NET CLI:

```
dotnet build
```


# Executing the tests
The test optimization problems can be executed by running tests in the project:

```
dotnet test
```


# C implementation
The __c__ folder contains a performance-focused C implementation of the Differential Evolution algorithm as a single header (__c/de.h__), with no dependencies. It keeps the observable behaviour of the C# version (selection distribution, survival rules, Pareto fronts, truncation order), but uses flat arrays, does not allocate inside the generation loop, draws donors in O(1), and uses ENS-BS non-dominated sorting and radix sorts. The header comment lists every difference. The C#-style donor selection is available too (`DE_SELECT_SHUFFLE`, or `--selection shuffle` in the benchmark), so the effect of that change can be measured on its own.

```
cd c
make check                                   # tests
./bench --scenario sphere_d30_p500_g200      # benchmark one scenario (./bench --list shows all)
./tui                                        # watch the optimisation run in the terminal
```

The terminal UI shows the population (or the Pareto front for multi-objective problems), the best fitness per generation, and a panel where the problem, dimensions, population size, CR, F, number of generations, donor selection, seed and speed can be changed while it runs.


# Want to help?

Pull requests are very welcome. Some ideas could be:

- Implementing other optimization algorithms based on the generic framework described above.
- Performance optimizations
- Implementing a better scattering measure in the base algorithm class (__EvolutionaryAlgorithm__)
- Graphical visualization of optimization runs
- \+ all the things I haven't thought of :-)






