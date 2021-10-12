using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    public class EvaluatedIndividual : Individual, IEvaluatedIndividual
    {
        public ImmutableList<double> FitnessValues { get; }

        public EvaluatedIndividual(Individual individual, ImmutableList<double> fitnessValues) : base(individual.Genes.ToArray())
        {
            FitnessValues = fitnessValues;
        }

        public ParetoEvaluatedIndividual AddParetoRank(int paretoRank) => new ParetoEvaluatedIndividual(this, paretoRank);

        public override string ToString()
            => $"{base.ToString()} --> {(string.Join(";", FitnessValues.Select(g => g.ToString())))}";
    }

}