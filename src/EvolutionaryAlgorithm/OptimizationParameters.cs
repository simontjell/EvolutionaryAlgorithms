using System.Collections.Generic;
using EvolutionaryAlgorithm.TerminationCriteria;

namespace EvolutionaryAlgorithm
{
    public class OptimizationParameters(int populationSize, params ITerminationCriterion[] terminationCriteria)
    {
        public int PopulationSize { get; } = populationSize;
        public IEnumerable<ITerminationCriterion> TerminationCriteria { get; } = terminationCriteria;
    }
}