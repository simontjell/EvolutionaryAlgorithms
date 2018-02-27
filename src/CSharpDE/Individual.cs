using System.Collections.Immutable;
using System.Linq;

namespace CSharpDE
{
    public class Individual
    {
        public ImmutableList<double> Genes { get; }
        public double this[int index] => Genes[index];

        public Individual(params double[] genes)
        {
            Genes = genes.ToImmutableList();
        }

        public EvaluatedIndividual AddFitnessValues(ImmutableList<double> fitnessValues) => new EvaluatedIndividual(this, fitnessValues);

        public override string ToString()
            => $"[{(string.Join(";", Genes.Select(g => g.ToString())))}]";
    }
}