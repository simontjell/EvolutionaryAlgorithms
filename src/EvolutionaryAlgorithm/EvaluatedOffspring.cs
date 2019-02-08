using System.Linq;
using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public class EvaluatedOffspring : Offspring, IEvaluatedIndividual
    {
        public ImmutableList<double> FitnessValues { get; private set; }

        public EvaluatedOffspring(Offspring offspring, ImmutableList<double> fitnessValues) : base(offspring.Parents, offspring.Genes.ToArray())
        {
            FitnessValues = fitnessValues;
        }
    }
}