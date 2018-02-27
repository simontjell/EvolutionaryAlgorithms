namespace CSharpDE
{
    public class ParetoEvaluatedIndividual : EvaluatedIndividual
    {
        public ParetoEvaluatedIndividual(EvaluatedIndividual individual, int paretoRank) : base(individual, individual.FitnessValues)
        {
            ParetoRank = paretoRank;
        }

        public int ParetoRank { get; }

        public override string ToString()
            => $"{base.ToString()} ({ParetoRank})";
    }

}