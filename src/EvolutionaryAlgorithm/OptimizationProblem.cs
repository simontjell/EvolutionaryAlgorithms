using System;
using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    public abstract class OptimizationProblem(int n, Random rnd) : IOptimizationProblem
    {
        protected readonly int _n = n;
        protected readonly Random _rnd = rnd;

        public abstract ImmutableList<double> CalculateFitnessValues(Individual individual);
        
        public virtual Individual CreateRandomIndividual()
            => new(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());

        public virtual bool IsFeasible(Individual individual) => true;
    }
}