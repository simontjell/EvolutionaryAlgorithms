using System;

namespace SimpleSystemer.EA
{
    public class LambdaTerminationCriterion : TerminationCriterion
    {
        private readonly Func<EvolutionaryAlgorithm, bool> _shouldTerminate;

        public LambdaTerminationCriterion(Func<EvolutionaryAlgorithm, bool> shouldTerminate)
        {
            _shouldTerminate = shouldTerminate;
        }

        public override bool ShouldTerminate(EvolutionaryAlgorithm optimizationAlgorithm) => _shouldTerminate(optimizationAlgorithm);
    }
}