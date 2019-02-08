using System.Collections.Immutable;

namespace SimpleSystemer.EA
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