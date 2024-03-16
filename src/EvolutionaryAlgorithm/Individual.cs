using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    public class Individual(params double[] genes)
    {
        public ImmutableList<double> Genes { get; } = [.. genes];
        public double this[int index] => Genes[index];

        public EvaluatedIndividual AddFitnessValues(ImmutableList<double> fitnessValues) => new(this, fitnessValues);

        public override string ToString()
            => $"[{(string.Join(";", Genes.Select(g => g.ToString())))}]";
    }
}