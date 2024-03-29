﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class RosenbrockOptimizationProblem : OptimizationProblem
    {
        public RosenbrockOptimizationProblem(int n, Random rnd) : base(n, rnd)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(n, 2);
        }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => new List<double> { 
                Enumerable
                .Range(0, _n-1)
                .Select(i => 
                    100.0*Math.Pow(individual.Genes[i+1]-Math.Pow(individual.Genes[i], 2.0), 2.0) 
                    + Math.Pow(1.0-individual.Genes[i], 2.0)
                )
                .Sum()
            }.ToImmutableList();

        public override Individual CreateRandomIndividual() 
            => new(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());
    }
}
