using System.Collections.Immutable;

namespace CSharpDE
{
    public class Individual
    {
        public readonly ImmutableList<double> Genes;
        public double this[int index] => Genes[index];

        public Individual(params double[] genes)
        {
            Genes = genes.ToImmutableList();
        }
    }
}