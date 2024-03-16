using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class SphereOptimizationProblem(int n, Random rnd) : OptimizationProblem(n, rnd)
    {
        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => [individual.Genes.Sum(g => Math.Pow(g, 2.0))];

        public override Individual CreateRandomIndividual() 
            => new(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());
    }
}
