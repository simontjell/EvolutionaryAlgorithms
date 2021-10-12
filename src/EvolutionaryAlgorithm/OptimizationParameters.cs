using System.Collections.Generic;
using EvolutionaryAlgorithm.TerminationCriteria;

namespace EvolutionaryAlgorithm
{
    public class OptimizationParameters
    {
        public OptimizationParameters(int populationSize, params ITerminationCriterion[] terminationCriteria)
        {
            PopulationSize = populationSize;
            TerminationCriteria = terminationCriteria;
        }

        public int PopulationSize { get; private set; }
        public IEnumerable<ITerminationCriterion> TerminationCriteria { get; private set; }
    }
}