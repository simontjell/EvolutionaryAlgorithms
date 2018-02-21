namespace CSharpDE
{
    public abstract class OptimizationProblem
    {
        public abstract double CalculateFitnessValue(Individual individual);
        public abstract Individual CreateRandomIndividual();
        public virtual bool IsFeasible(Individual individual) => true;
    }
}