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
    // Command settings specific to Sphere problem
    public class SphereSettings : OptimizationSettingsBase
    {
        [Description("Problem dimensions")]
        [CommandOption("-d|--dimensions")]
        [DefaultValue(2)]
        public int Dimensions { get; init; } = 2;
    }

    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class SphereOptimizationProblem(int n, Random rnd) : OptimizationProblem(n, rnd), IOptimizationProblemCommand<SphereSettings>
    {
        public static string CommandName => "sphere";
        public static string? Description => "Sphere optimization problem - minimize sum of squared variables";
        public static string[]? Aliases => new[] { "sphere-problem" };

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => [individual.Genes.Sum(g => Math.Pow(g, 2.0))];

        public override Individual CreateRandomIndividual() 
            => new(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble() * 10 - 5).ToArray()); // Range [-5, 5]

        public static IOptimizationProblem CreateOptimizationProblem(SphereSettings settings, Random random)
        {
            return new SphereOptimizationProblem(settings.Dimensions, random);
        }

        public static AlgorithmParameters GetAlgorithmParameters(SphereSettings settings)
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
