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
    // Command settings specific to Booth problem
    public class BoothSettings : OptimizationSettingsBase
    {
        [Description("Search range (±range)")]
        [CommandOption("--range")]
        [DefaultValue(10.0)]
        public double Range { get; init; } = 10.0;
    }

    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class BoothOptimizationProblem : OptimizationProblem, IOptimizationProblemCommand<BoothSettings>
    {
        private readonly double _range;

        public BoothOptimizationProblem(double range, Random rnd) : base(2, rnd)
        {
            _range = range;
        }

        // For backward compatibility with existing usage
        public BoothOptimizationProblem(Random rnd) : this(10.0, rnd) { }

        public static string CommandName => "booth";
        public static string? Description => "Booth optimization problem - global minimum at (1,3) with value 0";
        public static string[]? Aliases => new[] { "booth-function" };

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => [CalculateFitnessValue(individual.Genes[0], individual.Genes[1])];

        private static double CalculateFitnessValue(double x, double y)
            => Math.Pow(x+2.0*y-7.0, 2.0) + Math.Pow(2.0*x+y-5.0, 2.0);

        public override bool IsFeasible(Individual individual)
            => individual.Genes[0] >= -1  && individual.Genes[1] <= 10;

        public override Individual CreateRandomIndividual()
            => new(new[] { 
                _rnd.NextDouble() * 2 * _range - _range, // x in [-range, range]
                _rnd.NextDouble() * 2 * _range - _range  // y in [-range, range]
            });

        public static IOptimizationProblem CreateOptimizationProblem(BoothSettings settings, Random random)
        {
            return new BoothOptimizationProblem(settings.Range, random);
        }

        public static AlgorithmParameters GetAlgorithmParameters(BoothSettings settings)
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
