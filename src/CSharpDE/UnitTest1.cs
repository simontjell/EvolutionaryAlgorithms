using System;
using System.Collections;
using System.Collections.Generic;
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

    public class OptimizationProblem<TIndividual, TFitnessFunction, TGene> 
        where TFitnessFunction : IFitnessFunction<TIndividual>
        where TIndividual : IIndividual<TGene>
    {
        private readonly TFitnessFunction _fitnessFunction;
        private readonly List<GeneConfiguration<TGene>> _geneConfigurations;

        public OptimizationProblem(TFitnessFunction fitnessFunction, List<GeneConfiguration<TGene>> geneConfigurations)
        {
            _fitnessFunction = fitnessFunction;
            _geneConfigurations = geneConfigurations;
        }
    }

    public interface IIndividual<TGene>
    {
        IList<TGene> Genes { get; }
    }

    public interface IFitnessFunction<TIndividual>
    {
        double Evaluate(TIndividual individual);
    }
    
    public class GeneConfiguration<TGene>
    {
    }

    public class MyIndividual : IIndividual<double>
    {
        public IList<double> Genes { get; }
    }

    public class MyFitnessFunction<MyIndividual> : IFitnessFunction<MyIndividual>
    {
        public double Evaluate(MyIndividual individual)
        {
            throw new NotImplementedException();
        }
    }


}