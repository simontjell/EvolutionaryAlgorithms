using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public interface IOptimizationProblem
    {
        ImmutableList<double> CalculateFitnessValue(Individual individual);
        Individual CreateRandomIndividual();
        bool IsFeasible(Individual individual);
    }
}