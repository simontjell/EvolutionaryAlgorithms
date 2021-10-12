using System;
using EvolutionaryAlgorithm.TerminationCriteria;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution
{
    public class DifferentialEvolutionOptimizationParameters : OptimizationParameters
    {
        public double CR { get; private set; } = 0.5;
        public double F { get; private set; } = 1.0;

        public DifferentialEvolutionOptimizationParameters(int populationSize, params ITerminationCriterion[] terminationCriteria) : base(populationSize, terminationCriteria)
        {
            if (populationSize < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(populationSize), "The population must consist of at least 4 individuals");
            }
        }
    }
}