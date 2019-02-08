using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public abstract class OptimizationProblem
    {
        public abstract ImmutableList<double> CalculateFitnessValue(Individual individual);
        public abstract Individual CreateRandomIndividual();
        public virtual bool IsFeasible(Individual individual) => true;
    }
}