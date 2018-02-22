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
                            new MyOptimizationProblem(),
                            new DifferentialEvolutionOptimizationParameters(
                                100,
                                new LambdaTerminationCriterion(algorithm => algorithm.Generations.Last().Population.Any(individual => algorithm.FitnessValues[individual].Single() == 0.0))  // TODO: Rethink the interface for getting best fit individual(s)
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

    }
}
