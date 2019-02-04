namespace CSharpDE
{
    public abstract class TerminationCriterion
    {
        public abstract bool ShouldTerminate(EvolutionaryAlgorithm optimizationAlgorithm);
    }
}