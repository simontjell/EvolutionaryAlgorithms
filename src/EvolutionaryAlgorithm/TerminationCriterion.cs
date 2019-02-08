namespace SimpleSystemer.EA
{
    public abstract class TerminationCriterion
    {
        public abstract bool ShouldTerminate(EvolutionaryAlgorithm optimizationAlgorithm);
    }
}