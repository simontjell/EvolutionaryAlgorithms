using System;
using DifferentialEvolution.Tests.OptimizationProblems;
using Xunit;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithm.TerminationCriteria;
using System.Linq;
using System.Collections.Immutable;

namespace DifferentialEvolution.Tests
{
    public class OptimizationProblemTests
    {
        [Fact]
        public void When_optimization_problem_is_Booth_DE_converges_correctly()
        {
            // Arrange
            var rnd = new Random(0);
            var sut = new DifferentialEvolution(
                new BoothOptimizationProblem(rnd),
                new DifferentialEvolutionOptimizationParameters(
                    100,
                    new GenerationCountTerminationCriterion(100)
                ),
                rnd
            );

            // Act
            EvaluatedIndividual GetBestIndividual()
                => sut.GetBestIndividuals(sut.Generations.Last()).Single();

            sut.OnGenerationFinished += (s, e) => {
                var bestIndividual = GetBestIndividual();
                Console.WriteLine($"{nameof(BoothOptimizationProblem)} {bestIndividual}");
            };

            sut.Optimize();

            // Assert
            var finalBestIndividual = GetBestIndividual();
            Assert.Equal(1.0, finalBestIndividual.Genes[0], 2);
            Assert.Equal(3.0, finalBestIndividual.Genes[1], 2);
            Assert.Equal(0.0, finalBestIndividual.FitnessValues.Single(), 2);
        }

        [Fact]
        public void When_optimization_problem_is_Sphere_DE_converges_correctly()
        {
            // Arrange
            var rnd = new Random(0);
            var sut = new DifferentialEvolution(
                new SphereOptimizationProblem(2, rnd),
                new DifferentialEvolutionOptimizationParameters(
                    100,
                    new GenerationCountTerminationCriterion(100)
                ),
                rnd
            );

            // Act
            EvaluatedIndividual GetBestIndividual()
                => sut.GetBestIndividuals(sut.Generations.Last()).Single();

            sut.OnGenerationFinished += (s, e) => {
                var bestIndividual = GetBestIndividual();
                Console.WriteLine($"{nameof(SphereOptimizationProblem)} {sut.Generations.Count}: {bestIndividual}");
            };

            sut.Optimize();

            // Assert
            var finalBestIndividual = GetBestIndividual();
            Assert.Equal(0.0, finalBestIndividual.Genes[0], 2);
            Assert.Equal(0.0, finalBestIndividual.Genes[1], 2);
            Assert.Equal(0.0, finalBestIndividual.FitnessValues.Single(), 2);
        }

        [Fact]
        public void When_optimization_problem_is_Schaffer_DE_converges_correctly()
        {
            // Arrange
            var rnd = new Random(0);
            var populationSize = 100;
            var sut = new DifferentialEvolution(
                new SchafferFunctionOptimizationProblem(rnd),
                new DifferentialEvolutionOptimizationParameters(
                    populationSize,
                    new GenerationCountTerminationCriterion(100)
                ),
                rnd
            );

            // Act
            IImmutableList<ParetoEvaluatedIndividual> GetBestIndividuals()
                => sut.GetBestIndividuals(sut.Generations.Last());

            sut.OnGenerationFinished += (s, e) => {
                var bestIndividuals = GetBestIndividuals();
                var paretoFronts = sut.Generations.Last().Population.GroupBy(p => p.ParetoRank).Select(g => new { ParetoRank = g.Key, NumberOfIndividuals = g.Count() } ).OrderBy(p => p.ParetoRank).ToList();
                Console.WriteLine($"{nameof(SchafferFunctionOptimizationProblem)} {sut.Generations.Count}: {string.Join(", ", paretoFronts.Select(p => $"{p.ParetoRank}: {p.NumberOfIndividuals}"))}");
            };

            sut.Optimize();
            System.IO.File.WriteAllLines("schaffer_front_0.csv", finalBestIndividuals.Select(p => $"{p.FitnessValues[0]},{p.FitnessValues[1]}"));

            // Assert
            var finalBestIndividuals = GetBestIndividuals();
            Assert.Equal(0, finalBestIndividuals.Select(i => i.ParetoRank).Distinct().Single());
            Assert.True(finalBestIndividuals.Count >= populationSize / 2);
            Assert.Equal(populationSize, sut.Generations.Last().Population.Count);
            Assert.True(finalBestIndividuals.Where(i => i.FitnessValues[0] > 1.0).All(i => i.FitnessValues[1] < 1.0));
            Assert.True(finalBestIndividuals.Where(i => i.FitnessValues[1] > 1.0).All(i => i.FitnessValues[0] < 1.0));
        }

    }
}