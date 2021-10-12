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
            var sut = new DifferentialEvolution(
                new BoothOptimizationProblem(),
                new DifferentialEvolutionOptimizationParameters(
                    100,
                    new GenerationCountTerminationCriterion(100)
                ),
                new Random(0)
            );

            // Act
            EvaluatedIndividual bestIndividual = null; 
            sut.OnGenerationFinished += (s, e) => {
                bestIndividual = sut.GetBestIndividuals(sut.Generations.Last()).Single();
                Console.WriteLine($"{sut.Generations.Count}: ({bestIndividual.Genes[0]}, {bestIndividual.Genes[1]}) --> {bestIndividual.FitnessValues[0]}");
            };

            sut.Optimize();

            // Assert
            Assert.Equal(1.0, bestIndividual.Genes[0], 2);
            Assert.Equal(3.0, bestIndividual.Genes[1], 2);
            Assert.Equal(0.0, bestIndividual.FitnessValues.Single(), 2);
        }
    }
}