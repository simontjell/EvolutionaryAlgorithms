using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using System.Collections.Immutable;

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

    public abstract class EvolutionaryAlgorithm
    {
        private readonly OptimizationProblem _optimizationProblem;
        private readonly OptimizationParameters _optimizationParameters;

        public ImmutableList<Generation> Generations { get; protected set; }

        public ImmutableDictionary<Individual, double> FitnessValues { get; protected set; };

        protected EvolutionaryAlgorithm(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters)
        {
            _optimizationProblem = optimizationProblem;
            _optimizationParameters = optimizationParameters;


        }

        // https://en.wikipedia.org/wiki/Evolutionary_algorithm
        public virtual void Optimize()
        {
            // Step One: Generate the initial population of individuals randomly. (First generation)
            Generations = new List<Generation>{ InitializeFirstGeneration() }.ToImmutableList();

            // Step Two: Evaluate the fitness of each individual in that population(time limit, sufficient fitness achieved, etc.)
            FitnessValues = Generations.Single().Population.ToImmutableDictionary(individual => individual, individual => _optimizationProblem.CalculateFitnessValue(individual));

            // Step Three: Repeat the following regenerational steps until termination:

            // Select the best - fit individuals for reproduction. (Parents)
            // Breed new individuals through crossover and mutation operations to give birth to offspring.
            // Evaluate the individual fitness of new individuals.
            // Replace least - fit population with new individuals.

            throw new NotImplementedException();
        }

        protected virtual Generation InitializeFirstGeneration()
        {
            return new Generation
            {
                Population =
                    Enumerable.Range(0, _optimizationParameters.PopulationSize)
                    .Select(i => _optimizationProblem.CreateRandomIndividual())
                    .ToList()
            };
        }
    }



    public class OptimizationParameters
    {
        public int PopulationSize { get; set; }
    }

    public class DifferentialEvolution : EvolutionaryAlgorithm
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

    public class Generation
    {
        public List<Individual> Population { get; set; }
    }
}