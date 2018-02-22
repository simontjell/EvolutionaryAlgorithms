using System.Collections.Immutable;

namespace CSharpDE
{
    public class Generation
    {
        public Generation(ImmutableList<Individual> population)
        {
            Population = population;
        }

        public ImmutableList<Individual> Population { get; private set; }
    }
}