namespace EvolutionaryAlgorithm.TerminationCriteria
{
    public interface ITerminationCriterion
    {
        bool ShouldTerminate(IEvolutionaryAlgorithm optimizationAlgorithm);
    }
}