namespace SimpleSystemer.EA.TerminationCriteria
{
    public interface ITerminationCriterion
    {
        bool ShouldTerminate(IEvolutionaryAlgorithm optimizationAlgorithm);
    }
}