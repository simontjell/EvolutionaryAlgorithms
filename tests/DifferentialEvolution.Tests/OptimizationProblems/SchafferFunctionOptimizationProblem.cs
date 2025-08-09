using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using Spectre.Console.Cli;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // Command settings specific to Schaffer problem
    public class SchafferSettings : OptimizationSettingsBase
    {
        [Description("Range parameter (minimum 10.0)")]
        [CommandOption("-a|--range")]
        [DefaultValue(10.0)]
        public double Range { get; init; } = 10.0;
    }

    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_multi-objective_optimization
    public class SchafferFunctionOptimizationProblem : OptimizationProblem, IOptimizationProblemCommand<SchafferSettings>
    {
        private const double MinimumA = 10.0;
        private readonly double _a;

        public SchafferFunctionOptimizationProblem(Random rnd, double a = MinimumA) : base(2, rnd)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(a, MinimumA);
            _a = a;
        }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual)
            => [Math.Pow(individual[0], 2.0), Math.Pow(individual[0] - 2, 2.0)];

        public override Individual CreateRandomIndividual()
            => new(-_a + (_rnd.NextDouble() * 2.0 * _a), -_a + (_rnd.NextDouble() * 2.0 * _a));

        public override bool IsFeasible(Individual individual)
            => individual.Genes[0] >= -_a && individual.Genes[0] <= _a;

        public static string CommandName => "schaffer";
        public static string? Description => "Schaffer function optimization problem - multi-objective test function";
        public static string[]? Aliases => new[] { "schaffer-function" };

        public static IOptimizationProblem CreateOptimizationProblem(SchafferSettings settings, Random random)
        {
            return new SchafferFunctionOptimizationProblem(random, settings.Range);
        }

        public static AlgorithmParameters GetAlgorithmParameters(SchafferSettings settings)
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
