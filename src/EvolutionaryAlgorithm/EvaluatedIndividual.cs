using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    public class EvaluatedIndividual(Individual individual, ImmutableList<double> fitnessValues) : Individual(individual.Genes.ToArray()), IEvaluatedIndividual
    {
        public ImmutableList<double> FitnessValues { get; } = fitnessValues;

        public ParetoEvaluatedIndividual AddParetoRank(int paretoRank) => new ParetoEvaluatedIndividual(this, paretoRank);

        public override string ToString()
            => $"{base.ToString()} --> {string.Join(";", FitnessValues.Select(g => g.ToString()))}";
    }

}