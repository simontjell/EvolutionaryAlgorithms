using System;

namespace CSharpDE
{
    public class GenerationCountTerminationCriterion : TerminationCriterion
    {
        private readonly long _numberOfGenerations;

        public GenerationCountTerminationCriterion(long numberOfGenerations) => (_numberOfGenerations) = (numberOfGenerations);

        public override bool ShouldTerminate(EvolutionaryAlgorithm optimizationAlgorithm) => optimizationAlgorithm.Generations.Count >= _numberOfGenerations;
    }
}