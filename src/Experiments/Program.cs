using CSharpDE;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Experiments
{
    class Program
    {
        static void Main(string[] args)
        {
            var optimizationAlgorithm = new DifferentialEvolution(
                new SchafferFunctionOptimizationProblem(),
                    new DifferentialEvolutionOptimizationParameters(
                        100,
                        new LambdaTerminationCriterion(algorithm => algorithm.GetBestIndividuals(algorithm.Generations.Last()).Single().FitnessValues.Single() == 0.0)  // TODO: Rethink the interface for getting best fit individual(s)
                    )
                );

            optimizationAlgorithm.Optimize();
        }

        public class MyOptimizationProblem : OptimizationProblem
        {
            private readonly Random _rnd;

            public MyOptimizationProblem()
            {
                _rnd = new Random((int)DateTime.Now.Ticks);
            }

            public override bool IsFeasible(Individual individual) => true;
            public override ImmutableList<double> CalculateFitnessValue(Individual individual) => new List<double> { Math.Pow(individual[0] * individual[1], 2.0) }.ToImmutableList();
            public override Individual CreateRandomIndividual() => new Individual(_rnd.NextDouble(), _rnd.NextDouble());
        }

        // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_multi-objective_optimization
        public class SchafferFunctionOptimizationProblem : OptimizationProblem
        {
            private readonly Random _rnd;

            public SchafferFunctionOptimizationProblem()
            {
                _rnd = new Random((int)DateTime.Now.Ticks);
            }

            public override ImmutableList<double> CalculateFitnessValue(Individual individual)
            {
                return new List<double> { 
                    Math.Pow(individual[0], 2.0),
                    Math.Pow(individual[0] - 2, 2.0)
                }.ToImmutableList();
            }

            public override Individual CreateRandomIndividual()
            {
                return new Individual(_rnd.NextDouble() * 10.0, _rnd.NextDouble() * 10.0);
            }
        }

    }
}
