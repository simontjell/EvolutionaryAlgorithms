using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public class Generation(IImmutableList<ParetoEvaluatedIndividual> population)
    {
        public IImmutableList<ParetoEvaluatedIndividual> Population { get; } = population;
    }
}