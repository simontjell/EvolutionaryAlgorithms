using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public interface IOptimizationProblem
    {
        ImmutableList<double> CalculateFitnessValues(Individual individual);
        Individual CreateRandomIndividual();
        bool IsFeasible(Individual individual);
    }
}