using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using DifferentialEvolution.Tests.OptimizationProblems;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithm.TerminationCriteria;
using Xunit;

namespace DifferentialEvolution.Tests
{
    public class DonorSelectionTests
    {
        [Fact]
        public void Donors_are_distinct_exclude_the_target_and_are_uniformly_distributed()
        {
            // Arrange
            const int populationSize = 7, exclude = 2, draws = 210_000;
            var rnd = new Random(0);
            var sut = new DifferentialEvolution(
                new SphereOptimizationProblem(2, rnd),
                new DifferentialEvolutionOptimizationParameters(populationSize, new GenerationCountTerminationCriterion(1)),
                rnd);
            var population = Enumerable.Range(0, populationSize)
                .Select(i => new Individual(i, 0).AddFitnessValues([i]).AddParetoRank(0))
                .ToArray();
            var selectDonors = typeof(DifferentialEvolution).GetMethod("SelectDonors", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var counts = new int[populationSize, populationSize, populationSize];

            // Act
            for (var k = 0; k < draws; k++)
            {
                var (a, b, c) = ((ParetoEvaluatedIndividual, ParetoEvaluatedIndividual, ParetoEvaluatedIndividual))selectDonors.Invoke(sut, [exclude, population])!;
                var (ia, ib, ic) = ((int)a.Genes[0], (int)b.Genes[0], (int)c.Genes[0]);

                // Assert: distinct and never the target
                Assert.True(ia != ib && ia != ic && ib != ic, $"Repeated donor: {ia}, {ib}, {ic}");
                Assert.True(ia != exclude && ib != exclude && ic != exclude, "Target selected as donor");
                counts[ia, ib, ic]++;
            }

            // Assert: every one of the 6*5*4 = 120 ordered triples is equally likely (chi-squared, df = 119)
            var allowed = Enumerable.Range(0, populationSize).Where(i => i != exclude).ToArray();
            var expected = draws / 120.0;
            var chi2 = (
                from a in allowed from b in allowed from c in allowed
                where a != b && a != c && b != c
                select Math.Pow(counts[a, b, c] - expected, 2) / expected
            ).Sum();
            Assert.True(chi2 < 180, $"Donor selection is not uniform (chi2 = {chi2:F1})");
        }

        [Fact]
        public void Minimum_population_size_works()
        {
            var rnd = new Random(0);
            var sut = new DifferentialEvolution(
                new SphereOptimizationProblem(2, rnd),
                new DifferentialEvolutionOptimizationParameters(4, new GenerationCountTerminationCriterion(50)),
                rnd);

            sut.Optimize();

            Assert.Equal(50, sut.Generations.Count);
            Assert.Equal(4, sut.Generations.Last().Population.Count);
        }
    }
}
