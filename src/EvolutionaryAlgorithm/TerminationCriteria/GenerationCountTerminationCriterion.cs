using System;

namespace EvolutionaryAlgorithm.TerminationCriteria
{
    public class GenerationCountTerminationCriterion : ITerminationCriterion
    {
        private readonly long _numberOfGenerations;

        public GenerationCountTerminationCriterion(long numberOfGenerations) => (_numberOfGenerations) = (numberOfGenerations);

        public bool ShouldTerminate(IEvolutionaryAlgorithm optimizationAlgorithm) => optimizationAlgorithm.Generations.Count >= _numberOfGenerations;
    }
}