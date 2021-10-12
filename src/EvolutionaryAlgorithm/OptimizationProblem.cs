using System;
using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    public abstract class OptimizationProblem : IOptimizationProblem
    {
        protected readonly int _n;
        protected readonly Random _rnd;

        public OptimizationProblem(int n)
        {
            _rnd = new Random((int)DateTime.Now.Ticks);

            _n = n;
        }
        public abstract ImmutableList<double> CalculateFitnessValues(Individual individual);
        public virtual Individual CreateRandomIndividual()
            => new Individual(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());

        public virtual bool IsFeasible(Individual individual) => true;
    }
}