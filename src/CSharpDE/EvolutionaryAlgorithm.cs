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

        // The comments below are taken from the pseudo code specification in https://en.wikipedia.org/wiki/Evolutionary_algorithm
        public virtual void Optimize()
        {
            // Step One: Generate the initial population of individuals randomly. (First generation)
            // Step Two: Evaluate the fitness of each individual in that population(time limit, sufficient fitness achieved, etc.)
            Generations = new List<Generation> { InitializeFirstGeneration() }.ToImmutableList();

            // Step Three: Repeat the following regenerational steps until termination:
            while (ShouldContinue())
            {
                // Select the best - fit individuals for reproduction. (Parents)
                var parents = SelectParents();

                // Breed new individuals through crossover and mutation operations to give birth to offspring.
                var offspringIndividuals = CreateOffspring(parents);

                // Evaluate the individual fitness of new individuals.
                var evaluatedOffspringIndividuals = offspringIndividuals.Select(offspringIndividual => new EvaluatedOffspring(offspringIndividual, _optimizationProblem.CalculateFitnessValue(offspringIndividual))).ToImmutableList();

                // Replace least-fit population with new individuals.
                var newPopulation = new List<EvaluatedIndividual>();

                foreach (var evaluatedOffspringIndividual in evaluatedOffspringIndividuals)
                {
                    if (CheckOffspringSurvivalAndGetSurvivingParents(evaluatedOffspringIndividual, out var survivingParents))
                    {
                        newPopulation.Add(new EvaluatedIndividual(evaluatedOffspringIndividual, evaluatedOffspringIndividual.FitnessValues));
                    }

                    newPopulation.AddRange(survivingParents);
                }

                Generations = Generations.Add(new Generation(newPopulation.ToImmutableList()));

                Console.WriteLine(GetBestIndividuals(Generations.Last()).Single().FitnessValues.Single());
            }
        }

        protected virtual bool CheckOffspringSurvivalAndGetSurvivingParents(EvaluatedOffspring evaluatedOffspringIndividual, out ImmutableList<EvaluatedIndividual> survivingParents)
        {
            var survivingParentsList = new List<EvaluatedIndividual>();

            if (GetProblemDimensionality() == 1)
            {
                foreach (var parent in evaluatedOffspringIndividual.Parents)
                {
                    if (parent.FitnessValues.Single() < evaluatedOffspringIndividual.FitnessValues.Single())
                    {
                        survivingParentsList.Add(parent);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("TODO: Implement for multi-objective problems");
            }

            survivingParents = survivingParentsList.ToImmutableList();

            return survivingParents.Count < evaluatedOffspringIndividual.Parents.Count;
        }

        protected abstract Generation InitializeFirstGeneration();

        protected abstract bool ShouldContinue();

        protected abstract ImmutableList<Offspring> CreateOffspring(ImmutableList<EvaluatedIndividual> parents);    // TODO: Consider making this return EvaluatedOffspring instead
        protected abstract ImmutableList<EvaluatedIndividual> SelectParents();

        protected virtual int GetProblemDimensionality()
            => Generations.First().Population.First().FitnessValues.Count;  // TODO: Find a better way to detect problem dimensionality...

        public virtual ImmutableList<EvaluatedIndividual> GetBestIndividuals(Generation generation)
        {
            if (GetProblemDimensionality() == 1)
            {
                return 
                    generation
                    .Population
                    .OrderBy(individual => individual.FitnessValues.Single())
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
                .Select(i => new EvaluatedIndividual(i, _optimizationProblem.CalculateFitnessValue(i)))
                .ToImmutableList()
            );
        }

        protected override bool ShouldContinue()
            => _optimizationParameters.TerminationCriteria?.All(criterion => criterion.ShouldTerminate(this)) == false;
    }
}