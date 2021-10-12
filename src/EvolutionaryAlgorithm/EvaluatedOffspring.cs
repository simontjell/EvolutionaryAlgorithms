using System.Linq;
using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public class EvaluatedOffspring : Offspring, IEvaluatedIndividual
    {
        public ImmutableList<double> FitnessValues { get; }

        public EvaluatedOffspring(Offspring offspring, ImmutableList<double> fitnessValues) : base(offspring.Parents, offspring.Genes.ToArray())
        {
            FitnessValues = fitnessValues;
        }
    }
}