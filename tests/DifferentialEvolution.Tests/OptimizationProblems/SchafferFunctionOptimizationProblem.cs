using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_multi-objective_optimization
    public class SchafferFunctionOptimizationProblem : OptimizationProblem
    {
        private const double MinimumA = 10.0;
        private double _a;

        public SchafferFunctionOptimizationProblem(Random rnd, double a = MinimumA) : base(2, rnd)
        {
            if(a < MinimumA)
            {
                throw new ArgumentOutOfRangeException($"{nameof(a)} should be at least {MinimumA}");
            }
            _a = a;
        }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual)
            => new List<double> {
                Math.Pow(individual[0], 2.0),
                Math.Pow(individual[0] - 2, 2.0)
            }.ToImmutableList();

        public override Individual CreateRandomIndividual()
            => new Individual(-_a + (_rnd.NextDouble() * 2.0 * _a), -_a + (_rnd.NextDouble() * 2.0 * _a));

        public override bool IsFeasible(Individual individual)
            => individual.Genes[0] >= -_a && individual.Genes[0] <= _a;
    }

}
