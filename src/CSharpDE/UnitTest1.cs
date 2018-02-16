using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CSharpDE.Core;
using Xunit;

namespace CSharpDE
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
        }
    }

    public abstract class OptimizationProblem
    {
        protected abstract bool IsFeasible(Individual individual);
        protected abstract double CalculateFitnessValue(Individual individual);
        protected abstract Individual CreateRandomIndividual();
    }


    public class MyOptimizationProblem : OptimizationProblem
    {
        private readonly Random _rnd;

        public MyOptimizationProblem()
        {
            _rnd = new Random((int)DateTime.Now.Ticks);
        }
        
        protected override bool IsFeasible(Individual individual) => true;
        protected override double CalculateFitnessValue(Individual individual) => individual[0] * individual[1];
        protected override Individual CreateRandomIndividual() => new Individual(_rnd.NextDouble(), _rnd.NextDouble());
    }

    public class Individual
    {
        public List<double> Genes { get; private set; }
        public double this[int index] => Genes[index];

        public Individual(params double[] genes)
        {
            Genes = genes.ToList();
        }
    }
}