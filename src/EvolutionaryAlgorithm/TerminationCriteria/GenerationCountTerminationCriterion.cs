using System;

namespace EvolutionaryAlgorithm.TerminationCriteria
{
    public class GenerationCountTerminationCriterion(long numberOfGenerations) : ITerminationCriterion
    {
        public bool ShouldTerminate(IEvolutionaryAlgorithm optimizationAlgorithm) => optimizationAlgorithm.Generations.Count >= numberOfGenerations;
    }
}