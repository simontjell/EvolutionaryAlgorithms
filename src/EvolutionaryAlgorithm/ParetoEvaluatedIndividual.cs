namespace EvolutionaryAlgorithm
{
    public class ParetoEvaluatedIndividual(EvaluatedIndividual individual, int paretoRank) : EvaluatedIndividual(individual, individual.FitnessValues)
    {
        public int ParetoRank { get; } = paretoRank;

        public override string ToString()
            => $"{base.ToString()} ({ParetoRank})";
    }

}