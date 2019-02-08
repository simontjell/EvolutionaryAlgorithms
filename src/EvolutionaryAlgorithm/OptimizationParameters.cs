using System.Collections.Generic;
using SimpleSystemer.EA.TerminationCriteria;

namespace SimpleSystemer.EA
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