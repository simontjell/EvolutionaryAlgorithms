using System.Linq;
using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public class EvaluatedOffspring(Offspring offspring, ImmutableList<double> fitnessValues) : Offspring(offspring.Parents, offspring.Genes.ToArray()), IEvaluatedIndividual
    {
        public ImmutableList<double> FitnessValues { get; } = fitnessValues;
    }
}