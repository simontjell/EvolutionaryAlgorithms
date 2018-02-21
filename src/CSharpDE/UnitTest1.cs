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
            //var de = new DifferentialEvolution(
            //    new MyOptimizationProblem(), 
            //    new OptimizationParameters { PopulationSize = 100 }
            //);

            var optimizationAlgorithm = new NaiveOptimizationAlgorithm(
                new MyOptimizationProblem(),
                new OptimizationParameters { PopulationSize = 100 }
            );

            optimizationAlgorithm.Optimize();

        }
    }

    public class NaiveOptimizationAlgorithm : EvolutionaryAlgorithm
    {
        public NaiveOptimizationAlgorithm(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters){ }

        protected override ImmutableList<Offspring> CreateOffspring(ImmutableList<Individual> parents)
            => parents
               .Select(p => new Offspring(p, _optimizationProblem.CreateRandomIndividual()))
               .ToImmutableList();

        protected override bool ReplaceParents(Offspring offsprintIndividual, ImmutableDictionary<Individual, double> offspringFitnessValues)
            => offspringFitnessValues[offsprintIndividual] < offsprintIndividual.Parents.Select(p => FitnessValues[p]).Min();

        protected override ImmutableList<Individual> SelectParents()
            => Generations.Last().Population;
    }

    public abstract class OptimizationProblem
    {
        public abstract double CalculateFitnessValue(Individual individual);
        public abstract Individual CreateRandomIndividual();
        public virtual bool IsFeasible(Individual individual) => true;
    }

    public abstract class EvolutionaryAlgorithm
    {
        protected readonly OptimizationProblem _optimizationProblem;
        protected readonly OptimizationParameters _optimizationParameters;

        public ImmutableList<Generation> Generations { get; protected set; }

        public ImmutableDictionary<Individual, double> FitnessValues { get; protected set; }

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
            while (_optimizationParameters.TerminationCriteria?.All(criterion => criterion.ShouldTerminate(this)) ?? false == false)
            {
                var bestFitness = FitnessValues.Values.Min();

                // Select the best - fit individuals for reproduction. (Parents)
                var parents = SelectParents();

                // Breed new individuals through crossover and mutation operations to give birth to offspring.
                var offspring = CreateOffspring(parents);

                // Evaluate the individual fitness of new individuals.
                var offspringFitnessValues = offspring.Cast<Individual>().ToImmutableDictionary(individual => individual, individual => _optimizationProblem.CalculateFitnessValue(individual));

                // Replace least-fit population with new individuals.
                var newPopulation = new List<Individual>();
                var newFitnessValues = new List<KeyValuePair<Individual, double>>();

                foreach (var offsprintIndividual in offspring)
                {
                    if (ReplaceParents(offsprintIndividual, offspringFitnessValues))
                    {
                        newPopulation.Add(offsprintIndividual);
                        newFitnessValues.Add(new KeyValuePair<Individual, double>(offsprintIndividual, offspringFitnessValues[offsprintIndividual]));
                    }
                    else
                    {
                        newPopulation.AddRange(offsprintIndividual.Parents);
                    }
                }

                FitnessValues = FitnessValues.AddRange(newFitnessValues);
                Generations = Generations.Add(new Generation(newPopulation.ToImmutableList()));
            }

            throw new NotImplementedException();
        }

        protected abstract bool ReplaceParents(Offspring offsprintIndividual, ImmutableDictionary<Individual, double> offspringFitnessValues);
        protected abstract ImmutableList<Offspring> CreateOffspring(ImmutableList<Individual> parents);
        protected abstract ImmutableList<Individual> SelectParents();

        protected virtual Generation InitializeFirstGeneration()
        {
            return new Generation(
                Enumerable.Range(0, _optimizationParameters.PopulationSize)
                .Select(i => _optimizationProblem.CreateRandomIndividual())
                .ToImmutableList()
            );
        }
    }



    public class OptimizationParameters
    {
        public int PopulationSize { get; set; }
        public List<TerminationCriterion> TerminationCriteria { get; set; }
    }

    public abstract class TerminationCriterion
    {
        public abstract bool ShouldTerminate(EvolutionaryAlgorithm optimizationAlgorithm);
    }

    //public class DifferentialEvolution : EvolutionaryAlgorithm
    //{
    //    public DifferentialEvolution(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters){ }

    //    protected override ImmutableList<Individual> SelectParents()
    //        => Generations.Last().Population;   // Take all...

    //    protected override ImmutableList<Offspring> CreateOffspring(ImmutableList<Individual> parents)
    //        => throw new NotImplementedException();
    //}
    
    
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
        public readonly ImmutableList<double> Genes;
        public double this[int index] => Genes[index];

        public Individual(params double[] genes)
        {
            Genes = genes.ToImmutableList();
        }
    }

    public class Offspring : Individual
    {
        public ImmutableList<Individual> Parents { get; private set; }

        public Offspring(Individual parent, params double[] genes) : this(new List<Individual> { parent }.ToImmutableList(), genes) { }

        public Offspring(Individual parent, Individual individual) : this(parent, individual.Genes.ToArray()) { }

        public Offspring(ImmutableList<Individual> parents, params double[] genes) : base(genes)
        {
            Parents = parents;
        }

    }

    public class Generation
    {
        public Generation(ImmutableList<Individual> population)
        {
            Population = population;
        }

        public ImmutableList<Individual> Population { get; private set; }
    }
}