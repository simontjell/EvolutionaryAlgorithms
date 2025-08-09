using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using Spectre.Console.Cli;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // Command settings specific to Rosenbrock problem
    public class RosenbrockSettings : OptimizationSettingsBase
    {
        [Description("Problem dimensions")]
        [CommandOption("-d|--dimensions")]
        [DefaultValue(2)]
        public int Dimensions { get; init; } = 2;

        [Description("Scale factor 'a' parameter")]
        [CommandOption("--param-a")]
        [DefaultValue(1.0)]
        public double A { get; init; } = 1.0;

        [Description("Scale factor 'b' parameter")]
        [CommandOption("--param-b")]
        [DefaultValue(100.0)]
        public double B { get; init; } = 100.0;
    }

    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class RosenbrockOptimizationProblem : OptimizationProblem, IOptimizationProblemCommand<RosenbrockSettings>
    {
        private readonly double _a;
        private readonly double _b;

        public RosenbrockOptimizationProblem(int n, double a, double b, Random rnd) : base(n, rnd)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(n, 2);
            _a = a;
            _b = b;
        }

        // For backward compatibility with existing usage
        public RosenbrockOptimizationProblem(int n, Random rnd) : this(n, 1.0, 100.0, rnd) { }

        public static string CommandName => "rosenbrock";
        public static string? Description => "Rosenbrock optimization problem - classic test function with global minimum at (1,1)";
        public static string[]? Aliases => new[] { "banana", "rosenbrock-valley" };

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => new List<double> { 
                Enumerable
                .Range(0, _n-1)
                .Select(i => 
                    _b*Math.Pow(individual.Genes[i+1]-Math.Pow(individual.Genes[i], 2.0), 2.0) 
                    + Math.Pow(_a-individual.Genes[i], 2.0)
                )
                .Sum()
            }.ToImmutableList();

        public override Individual CreateRandomIndividual() 
            => new(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble() * 4 - 2).ToArray()); // Range [-2, 2]

        public static IOptimizationProblem CreateOptimizationProblem(RosenbrockSettings settings, Random random)
        {
            return new RosenbrockOptimizationProblem(settings.Dimensions, settings.A, settings.B, random);
        }

        public static AlgorithmParameters GetAlgorithmParameters(RosenbrockSettings settings)
        {
            return new AlgorithmParameters
            {
                PopulationSize = settings.PopulationSize,
                MaxGenerations = settings.MaxGenerations,
                CrossoverRate = settings.CrossoverRate,
                DifferentialWeight = settings.DifferentialWeight,
                Seed = settings.Seed,
                Verbose = settings.Verbose,
                OutputCsv = settings.OutputCsv,
                UseUI = settings.UseUI
            };
        }
    }
}
