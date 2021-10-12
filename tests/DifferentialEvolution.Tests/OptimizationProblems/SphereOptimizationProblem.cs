using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class SphereOptimizationProblem : OptimizationProblem
    {
        public SphereOptimizationProblem(int n, Random rnd) : base(n, rnd) {}

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => new List<double> { individual.Genes.Sum(g => Math.Pow(g, 2.0)) }.ToImmutableList();

        public override Individual CreateRandomIndividual() 
            => new Individual(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());
    }
}
