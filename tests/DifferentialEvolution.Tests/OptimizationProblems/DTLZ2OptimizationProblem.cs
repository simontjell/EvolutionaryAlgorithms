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
    // Command settings specific to DTLZ2 problem
    public class DTLZ2Settings : OptimizationSettingsBase
    {
        [Description("Number of objectives (minimum 2, default 3)")]
        [CommandOption("-m|--objectives")]
        [DefaultValue(3)]
        public int Objectives { get; init; } = 3;

        [Description("Number of variables (minimum m, default 12)")]
        [CommandOption("-n|--variables")]
        [DefaultValue(12)]
        public int Variables { get; init; } = 12;
    }

    // DTLZ2 is a scalable multi-objective test function with a spherical Pareto front
    // It can have any number of objectives (M >= 2)
    // The Pareto front forms a unit sphere in the positive orthant
    // 
    // This is an important benchmark because:
    // - It's scalable to any number of objectives
    // - The Pareto front has a well-defined geometric shape (sphere)
    // - It tests algorithm's ability to maintain diversity in higher dimensions
    // - The concave spherical surface is challenging for many algorithms
    public class DTLZ2OptimizationProblem : OptimizationProblem, IOptimizationProblemCommand<DTLZ2Settings>
    {
        private readonly int _objectives;
        private readonly int _variables;
        private readonly int _k; // k = n - m + 1

        public DTLZ2OptimizationProblem(Random rnd, int objectives = 3, int variables = 12) : base(variables, rnd)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(objectives, 2);
            ArgumentOutOfRangeException.ThrowIfLessThan(variables, objectives);
            
            _objectives = objectives;
            _variables = variables;
            _k = variables - objectives + 1;
        }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual)
        {
            var x = individual.Genes;
            var objectives = new double[_objectives];
            
            // Calculate g(x) = sum((x_i - 0.5)^2) for i = M to n
            var g = 0.0;
            for (int i = _objectives - 1; i < _variables; i++)
            {
                g += Math.Pow(x[i] - 0.5, 2.0);
            }
            
            // Calculate objectives
            for (int m = 0; m < _objectives; m++)
            {
                var f = 1.0 + g;
                
                // Product of cos terms
                for (int i = 0; i < _objectives - m - 1; i++)
                {
                    f *= Math.Cos(x[i] * Math.PI / 2.0);
                }
                
                // Sin term for all but the last objective
                if (m > 0)
                {
                    f *= Math.Sin(x[_objectives - m - 1] * Math.PI / 2.0);
                }
                
                objectives[m] = f;
            }
            
            return objectives.ToImmutableList();
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

        public static string CommandName => "dtlz2";
        public static string? Description => "DTLZ2 multi-objective test function - scalable benchmark with spherical concave Pareto front";
        public static string[]? Aliases => new[] { "dtlz-2" };

        public static IOptimizationProblem CreateOptimizationProblem(DTLZ2Settings settings, Random random)
        {
            return new DTLZ2OptimizationProblem(random, settings.Objectives, settings.Variables);
        }

        public static AlgorithmParameters GetAlgorithmParameters(DTLZ2Settings settings)
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