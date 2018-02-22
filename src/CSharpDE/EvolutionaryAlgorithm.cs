using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System;

namespace CSharpDE
{
    public abstract class EvolutionaryAlgorithm
    {
        protected readonly OptimizationProblem _optimizationProblem;

        public EvolutionaryAlgorithm(OptimizationProblem optimizationProblem)
        {
            _optimizationProblem = optimizationProblem;
        }

        public ImmutableList<Generation> Generations { get; protected set; }
        public ImmutableDictionary<Individual, ImmutableList<double>> FitnessValues { get; protected set; }

        // The comments below are taken from the pseudo code specification in https://en.wikipedia.org/wiki/Evolutionary_algorithm
        public virtual void Optimize()
        {
            // Step One: Generate the initial population of individuals randomly. (First generation)
            Generations = new List<Generation> { InitializeFirstGeneration() }.ToImmutableList();

            // Step Two: Evaluate the fitness of each individual in that population(time limit, sufficient fitness achieved, etc.)
            FitnessValues = Generations.Single().Population.ToImmutableDictionary(individual => individual, individual => _optimizationProblem.CalculateFitnessValue(individual));

            // Step Three: Repeat the following regenerational steps until termination:
            while (ShouldContinue())
            {
                // Select the best - fit individuals for reproduction. (Parents)
                var parents = SelectParents();

                // Breed new individuals through crossover and mutation operations to give birth to offspring.
                var offspringIndividuals = CreateOffspring(parents);

                // Evaluate the individual fitness of new individuals.
                var offspringFitnessValues = offspringIndividuals.Cast<Individual>().ToImmutableDictionary(individual => individual, individual => _optimizationProblem.CalculateFitnessValue(individual));

                // Replace least-fit population with new individuals.
                var newPopulation = new List<Individual>();
                var newFitnessValues = new List<KeyValuePair<Individual, ImmutableList<double>>>();

                foreach (var offspringIndividual in offspringIndividuals)
                {
                    if (ShouldReplaceParents(offspringIndividual, offspringFitnessValues))
                    {
                        newPopulation.Add(offspringIndividual);
                        newFitnessValues.Add(new KeyValuePair<Individual, ImmutableList<double>>(offspringIndividual, offspringFitnessValues[offspringIndividual]));
                    }
                    else
                    {
                        newPopulation.AddRange(offspringIndividual.Parents);
                    }
                }

                FitnessValues = FitnessValues.AddRange(newFitnessValues);
                Generations = Generations.Add(new Generation(newPopulation.ToImmutableList()));

                Console.WriteLine(FitnessValues[GetBestIndividuals(Generations.Last()).Single()].Single());
            }
        }

        protected abstract Generation InitializeFirstGeneration();

        protected abstract bool ShouldContinue();

        protected virtual bool ShouldReplaceParents(Offspring offsprintIndividual, ImmutableDictionary<Individual, ImmutableList<double>> offspringFitnessValues)
            =>
            GetProblemDimensionality() == 1 ?
                offspringFitnessValues[offsprintIndividual].Single() < offsprintIndividual.Parents.Select(p => FitnessValues[p].Single()).Min()
                :
                throw new NotImplementedException("TODO: Implement for multi-objective problems");

        protected abstract ImmutableList<Offspring> CreateOffspring(ImmutableList<Individual> parents);
        protected abstract ImmutableList<Individual> SelectParents();

        protected virtual int GetProblemDimensionality()
            => FitnessValues.Values.FirstOrDefault()?.Count ?? throw new NotImplementedException("Find a better way to detect problem dimensionality...");

        public virtual ImmutableList<Individual> GetBestIndividuals(Generation generation)
        {
            if (GetProblemDimensionality() == 1)
            {
                return 
                    generation
                    .Population
                    .OrderBy(individual => FitnessValues[individual].Single())
                    .Take(1)
                    .ToImmutableList();
            }
            else
            {
                // Pick the lowest Pareto rank
                throw new NotImplementedException();
            }
        }
    }

    public abstract class EvolutionaryAlgorithm<TOptimizationParameters> : EvolutionaryAlgorithm where TOptimizationParameters : OptimizationParameters
    {
        protected readonly TOptimizationParameters _optimizationParameters;

        protected EvolutionaryAlgorithm(OptimizationProblem optimizationProblem, TOptimizationParameters optimizationParameters) : base(optimizationProblem)
        {
            _optimizationParameters = optimizationParameters;
        }

        protected override Generation InitializeFirstGeneration()
        {
            return new Generation(
                Enumerable.Range(0, _optimizationParameters.PopulationSize)
                .Select(i => _optimizationProblem.CreateRandomIndividual())
                .ToImmutableList()
            );
        }

        protected override bool ShouldContinue()
            => _optimizationParameters.TerminationCriteria?.All(criterion => criterion.ShouldTerminate(this)) == false;
    }
}