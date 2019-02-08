using System;

namespace SimpleSystemer.EA.TerminationCriteria
{
    public class LambdaTerminationCriterion : ITerminationCriterion
    {
        private readonly Func<IEvolutionaryAlgorithm, bool> _shouldTerminate;

        public LambdaTerminationCriterion(Func<IEvolutionaryAlgorithm, bool> shouldTerminate)
        {
            _shouldTerminate = shouldTerminate;
        }

        public bool ShouldTerminate(IEvolutionaryAlgorithm optimizationAlgorithm) => _shouldTerminate(optimizationAlgorithm);
    }
}