using System.Collections.Generic;

namespace SimpleSystemer.EA
{
    public class OptimizationParameters
    {
        public OptimizationParameters(int populationSize, params TerminationCriterion[] terminationCriteria)
        {
            PopulationSize = populationSize;
            TerminationCriteria = terminationCriteria;
        }

        public int PopulationSize { get; private set; }
        public IEnumerable<TerminationCriterion> TerminationCriteria { get; private set; }
    }
}