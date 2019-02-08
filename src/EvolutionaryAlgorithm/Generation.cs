using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public class Generation
    {
        public Generation(ImmutableList<ParetoEvaluatedIndividual> population)
        {
            Population = population;
        }

        public ImmutableList<ParetoEvaluatedIndividual> Population { get; }
    }
}