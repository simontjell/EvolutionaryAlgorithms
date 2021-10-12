using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution.Tests.OptimizationProblems
{
    // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
    public class RosenbrockOptimizationProblem : OptimizationProblem
    {
        private readonly Random _rnd;
        private readonly int _n;

        public RosenbrockOptimizationProblem(int n) : base(n)
        {
            if(n < 2)
            {
                throw new ArgumentOutOfRangeException($"{nameof(n)} must be at least 2");
            }
        }

        public override ImmutableList<double> CalculateFitnessValues(Individual individual) 
            => new List<double> { 
                Enumerable
                .Range(0, _n-1)
                .Select(i => 
                    100.0*Math.Pow((individual.Genes[i+1]-Math.Pow(individual.Genes[i], 2.0)), 2.0) 
                    + Math.Pow(1.0-individual.Genes[i], 2.0)
                )
                .Sum()
            }.ToImmutableList();

        public override Individual CreateRandomIndividual() 
            => new Individual(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());
    }
}
