using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SimpleSystemer.EA;

namespace Experiments.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class BoothOptimizationProblem : OptimizationProblem
    {
        public BoothOptimizationProblem() : base(2) { }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => new List<double> { CalculateFitnessValue(individual.Genes[0], individual.Genes[1]) }.ToImmutableList();

        private double CalculateFitnessValue(double x, double y)
            => Math.Pow(x+2.0*y-7.0, 2.0) + Math.Pow(2.0*x+y-5.0, 2.0);
    }
}
