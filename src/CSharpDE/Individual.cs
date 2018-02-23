using System.Collections.Immutable;
using System.Linq;

namespace CSharpDE
{
    public class Individual
    {
        public ImmutableList<double> Genes { get; private set; }
        public double this[int index] => Genes[index];

        public Individual(params double[] genes)
        {
            Genes = genes.ToImmutableList();
        }
    }

    public class EvaluatedIndividual : Individual
    {
        public ImmutableList<double> FitnessValues { get; private set; }

        public EvaluatedIndividual(Individual individual, ImmutableList<double> fitnessValues) : base(individual.Genes.ToArray())
        {
            FitnessValues = fitnessValues;
        }
    }
}