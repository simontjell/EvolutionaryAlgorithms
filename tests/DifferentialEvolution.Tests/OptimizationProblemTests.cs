using System;
using DifferentialEvolution.Tests.OptimizationProblems;
using Xunit;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithm.TerminationCriteria;
using System.Linq;

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
                Console.WriteLine($"{sut.Generations.Count}: ({bestIndividual.Genes[0]}, {bestIndividual.Genes[1]}) --> {bestIndividual.FitnessValues[0]}");
            };

            sut.Optimize();

            // Assert
            var finalBestIndividual = GetBestIndividual();
            Assert.Equal(1.0, finalBestIndividual.Genes[0], 2);
            Assert.Equal(3.0, finalBestIndividual.Genes[1], 2);
            Assert.Equal(0.0, finalBestIndividual.FitnessValues.Single(), 2);
        }
    }
}