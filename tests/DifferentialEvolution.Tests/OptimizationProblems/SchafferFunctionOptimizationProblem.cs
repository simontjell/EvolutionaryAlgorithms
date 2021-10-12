using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_multi-objective_optimization
    public class SchafferFunctionOptimizationProblem : OptimizationProblem
    {
        public SchafferFunctionOptimizationProblem(Random rnd) : base(2, rnd){}

        public override ImmutableList<double> CalculateFitnessValues(Individual individual)
            => new List<double> {
                Math.Pow(individual[0], 2.0),
                Math.Pow(individual[0] - 2, 2.0)
            }.ToImmutableList();

        public override Individual CreateRandomIndividual()
            => new Individual(_rnd.NextDouble() * 10.0, _rnd.NextDouble() * 10.0);
    }

}
