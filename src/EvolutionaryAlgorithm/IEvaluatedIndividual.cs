using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public interface IEvaluatedIndividual
    {
        ImmutableList<double> FitnessValues { get; }
    }
}