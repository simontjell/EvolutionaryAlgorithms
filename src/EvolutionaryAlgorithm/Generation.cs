using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public class Generation
    {
        public Generation(IImmutableList<ParetoEvaluatedIndividual> population)
        {
            Population = population;
        }

        public IImmutableList<ParetoEvaluatedIndividual> Population { get; }
    }
}