using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SimpleSystemer.EA;

namespace Experiments.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class SphereOptimizationProblem : OptimizationProblem
    {
        private readonly Random _rnd;
        private readonly int _n;

        public SphereOptimizationProblem(int n)
        {
            _rnd = new Random((int)DateTime.Now.Ticks);
            _n = n;
        }

        public override bool IsFeasible(Individual individual) => true;
        public override ImmutableList<double> CalculateFitnessValue(Individual individual) => new List<double> { individual.Genes.Sum(g => Math.Pow(g, 2.0)) }.ToImmutableList();
        public override Individual CreateRandomIndividual() => new Individual(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());
    }
}
