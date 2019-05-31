using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public interface IOptimizationProblem
    {
        ImmutableList<double> CalculateFitnessValues(Individual individual);
        Individual CreateRandomIndividual();
        bool IsFeasible(Individual individual);
    }
}