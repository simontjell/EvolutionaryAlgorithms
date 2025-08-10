using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using Spectre.Console.Cli;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // Command settings specific to ZDT2 problem
    public class ZDT2Settings : OptimizationSettingsBase
    {
        [Description("Number of variables (minimum 2, default 30)")]
        [CommandOption("-n|--variables")]
        [DefaultValue(30)]
        public int Variables { get; init; } = 30;
    }

    // ZDT2 is a classic multi-objective test function with a CONCAVE Pareto front
    // f1(x) = x1
    // f2(x) = g(x) * h(f1, g(x))
    // where g(x) = 1 + 9 * sum(x_i for i=2 to n) / (n-1)
    // and h(f1, g) = 1 - (f1/g)^2  [NOTE: squared instead of sqrt, creating concave front]
    // 
    // The Pareto optimal solutions have x_i = 0 for i = 2, ..., n
    // and x1 ∈ [0, 1], giving a CONCAVE Pareto front
    // 
    // This is an important benchmark because it tests algorithm's ability to:
    // - Find and maintain diversity on a concave front
    // - Handle different curvature than convex fronts like ZDT1
    public class ZDT2OptimizationProblem : OptimizationProblem, IOptimizationProblemCommand<ZDT2Settings>
    {
        private readonly int _variables;

        public ZDT2OptimizationProblem(Random rnd, int variables = 30) : base(variables, rnd)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(variables, 2);
            _variables = variables;
        }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual)
        {
            var x = individual.Genes;
            
            // f1(x) = x1
            var f1 = x[0];
            
            // g(x) = 1 + 9 * sum(x_i for i=1 to n-1) / (n-1)
            var sum = 0.0;
            for (int i = 1; i < _variables; i++)
            {
                sum += x[i];
            }
            var g = 1.0 + 9.0 * sum / (_variables - 1);
            
            // h(f1, g) = 1 - (f1/g)^2  [CONCAVE: squared instead of sqrt]
            var h = 1.0 - Math.Pow(f1 / g, 2.0);
            
            // f2(x) = g(x) * h(f1, g(x))
            var f2 = g * h;
            
            return new[] { f1, f2 }.ToImmutableList();
        }

        public override Individual CreateRandomIndividual()
        {
            // All variables are in [0, 1]
            var genes = new double[_variables];
            for (int i = 0; i < _variables; i++)
            {
                genes[i] = _rnd.NextDouble(); // Random value in [0, 1]
            }
            return new Individual(genes);
        }

        public override bool IsFeasible(Individual individual)
        {
            // All variables must be in [0, 1]
            for (int i = 0; i < _variables; i++)
            {
                if (individual.Genes[i] < 0.0 || individual.Genes[i] > 1.0)
                    return false;
            }
            return true;
        }

        public static string CommandName => "zdt2";
        public static string? Description => "ZDT2 multi-objective test function - classic benchmark with CONCAVE Pareto front";
        public static string[]? Aliases => new[] { "zdt-2" };

        public static IOptimizationProblem CreateOptimizationProblem(ZDT2Settings settings, Random random)
        {
            return new ZDT2OptimizationProblem(random, settings.Variables);
        }

        public static AlgorithmParameters GetAlgorithmParameters(ZDT2Settings settings)
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