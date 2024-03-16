using System;

namespace EvolutionaryAlgorithm.TerminationCriteria
{
    public class LambdaTerminationCriterion(Func<IEvolutionaryAlgorithm, bool> shouldTerminate) : ITerminationCriterion
    {
        public bool ShouldTerminate(IEvolutionaryAlgorithm optimizationAlgorithm) => shouldTerminate(optimizationAlgorithm);
    }
}