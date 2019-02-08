using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public interface IEvaluatedIndividual
    {
        ImmutableList<double> FitnessValues { get; }
    }
}