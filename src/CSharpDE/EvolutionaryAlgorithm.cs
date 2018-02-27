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

        public event EventHandler OnGenerationFinished = null;

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
                        newPopulation.Add(evaluatedOffspringIndividual.AddFitnessValues(evaluatedOffspringIndividual.FitnessValues));
                    }

                    newPopulation.AddRange(survivingParents);
                }

                var paretoEvaluated = 
                    newPopulation
                    .Select(
                        evaluatedIndividual => 
                            evaluatedIndividual
                            .AddParetoRank(
                                CalculateParetoRank(evaluatedIndividual, newPopulation))
                    )
                    .ToImmutableList();

                var truncated = TruncatePopulation(paretoEvaluated);

                Generations = Generations.Add(new Generation(truncated));

                //Console.WriteLine(GetBestIndividuals(Generations.Last()).Single().FitnessValues.Single());
                //Console.WriteLine(
                //    string.Join(" | ",
                //        Generations.Last()
                //        .Population
                //        .GroupBy(p => p.ParetoRank, (paretoRank, individuals) => new { ParetoRank = paretoRank, Count = individuals.Count() })
                //        .OrderBy(p => p.ParetoRank)
                //        .Select(p => $"{p.ParetoRank}: {p.Count}")
                //    )
                //);

                OnGenerationFinished?.Invoke(this, null);
            }
        }

        private ImmutableList<ParetoEvaluatedIndividual> TruncatePopulation(ImmutableList<ParetoEvaluatedIndividual> paretoEvaluated)
            => paretoEvaluated.OrderBy(individual => individual.ParetoRank).ThenByDescending(individual => ScatteringMeasure(individual, paretoEvaluated)).ToImmutableList();

        // TODO: Find a good generic measure (e.g., average Euclidean distance in objective space to other individuals)
        protected double ScatteringMeasure(ParetoEvaluatedIndividual individual, ImmutableList<ParetoEvaluatedIndividual> population) 
            => population.Select(other => individual.Distance(other)).Min();   

        protected int CalculateParetoRank(EvaluatedIndividual evaluatedIndividual, IList<EvaluatedIndividual> newPopulation)
            => newPopulation.Aggregate(0, new Func<int, EvaluatedIndividual, int>((paretoRank, other) => paretoRank + (ParetoDominates(other, evaluatedIndividual) ? 1 : 0)));

        private bool ParetoDominates(IEvaluatedIndividual subject, IEvaluatedIndividual other)
        {
            var compared = subject.FitnessValues.Select(
                (fitnessValue, index) =>
                new
                {
                    SubjectBetter = fitnessValue < other.FitnessValues[index],
                    SubjectBetterOrEqual = fitnessValue <= other.FitnessValues[index],
                }
            );

            return compared.All(c => c.SubjectBetterOrEqual) && compared.Any(c => c.SubjectBetter);
        }

        protected virtual bool CheckOffspringSurvivalAndGetSurvivingParents(EvaluatedOffspring evaluatedOffspringIndividual, out ImmutableList<EvaluatedIndividual> survivingParents)
        {
            survivingParents = evaluatedOffspringIndividual.Parents.Where(p => ParetoDominates(p, evaluatedOffspringIndividual)).ToImmutableList();

            // Is this generic enough?
            return survivingParents.Count < evaluatedOffspringIndividual.Parents.Count;
        }

        protected abstract Generation InitializeFirstGeneration();

        protected abstract bool ShouldContinue();

        protected abstract ImmutableList<Offspring> CreateOffspring(ImmutableList<ParetoEvaluatedIndividual> parents);    // TODO: Consider making this return EvaluatedOffspring instead
        protected abstract ImmutableList<ParetoEvaluatedIndividual> SelectParents();

        protected virtual int GetProblemDimensionality()
            => Generations.First().Population.First().FitnessValues.Count;  // TODO: Find a better way to detect problem dimensionality...

        public virtual ImmutableList<ParetoEvaluatedIndividual> GetBestIndividuals(Generation generation)
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
                var lowestParetoRank = generation.Population.Min(i => i.ParetoRank);
                return generation.Population.Where(i => i.ParetoRank == lowestParetoRank).ToImmutableList();
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
            var evaluated = Enumerable.Range(0, _optimizationParameters.PopulationSize)
                .Select(i => _optimizationProblem.CreateRandomIndividual())
                .Select(i => i.AddFitnessValues(_optimizationProblem.CalculateFitnessValue(i)))
                .ToImmutableList();

            return new Generation(
                evaluated
                .Select(i => i.AddFitnessValues(_optimizationProblem.CalculateFitnessValue(i)).AddParetoRank(CalculateParetoRank(i, evaluated)))
                .ToImmutableList()
            );
        }

        protected override bool ShouldContinue()
            => _optimizationParameters.TerminationCriteria?.All(criterion => criterion.ShouldTerminate(this)) == false;
    }
}