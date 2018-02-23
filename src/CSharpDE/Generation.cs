using System.Collections.Immutable;

namespace CSharpDE
{
    public class Generation
    {
        public Generation(ImmutableList<EvaluatedIndividual> population)
        {
            Population = population;
        }

        public ImmutableList<EvaluatedIndividual> Population { get; private set; }
    }
}