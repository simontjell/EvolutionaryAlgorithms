using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CSharpDE
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var de = new DifferentialEvolution(
                new MyOptimizationProblem(), 
                new OptimizationParameters { PopulationSize = 100 }
            );
        }
    }

    public abstract class OptimizationProblem
    {
        public abstract double CalculateFitnessValue(Individual individual);
        public abstract Individual CreateRandomIndividual();
        public virtual bool IsFeasible(Individual individual) => true;
    }

    public abstract class OptimizationAlgorithm
    {
        private readonly OptimizationProblem _optimizationProblem;
        private readonly OptimizationParameters _optimizationParameters;

        protected OptimizationAlgorithm(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters)
        {
            _optimizationProblem = optimizationProblem;
            _optimizationParameters = optimizationParameters;

            Population = 
                Enumerable.Repeat<Func<Individual>>(_optimizationProblem.CreateRandomIndividual, _optimizationParameters.PopulationSize)
                .SelectInvoke()
                .ToList();
        }

        public List<Individual> Population { get; set; }
    }

    public class OptimizationParameters
    {
        public int PopulationSize { get; set; }
        public List<TerminationCriterion> TerminationCriteria { get; set; }
    }

    public abstract class TerminationCriterion
    {
        public abstract bool ShouldTerminate(OptimizationAlgorithm optimizationAlgorithm);
    }

    public class DifferentialEvolution : OptimizationAlgorithm
    {
        public DifferentialEvolution(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters)
        {
        }
    }
    
    
    public class MyOptimizationProblem : OptimizationProblem
    {
        private readonly Random _rnd;

        public MyOptimizationProblem()
        {
            _rnd = new Random((int)DateTime.Now.Ticks);
        }
        
        public override bool IsFeasible(Individual individual) => true;
        public override double CalculateFitnessValue(Individual individual) => individual[0] * individual[1];
        public override Individual CreateRandomIndividual() => new Individual(_rnd.NextDouble(), _rnd.NextDouble());
    }

    public class Individual
    {
        private readonly List<double> _genes;
        public double this[int index] => _genes[index];

        public Individual(params double[] genes)
        {
            _genes = genes.ToList();
        }
    }
}