using System.Collections.Immutable;

namespace CSharpDE
{
    public interface IEvaluatedIndividual
    {
        ImmutableList<double> FitnessValues { get; }
    }
}