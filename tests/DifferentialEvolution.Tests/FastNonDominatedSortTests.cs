using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionaryAlgorithm;
using Xunit;

namespace DifferentialEvolution.Tests
{
    public class FastNonDominatedSortTests
    {
        public static IEnumerable<object[]> Cases()
        {
            foreach (var objectives in new[] { 1, 2, 3 })
                foreach (var grid in new[] { 3, 1_000_000 })         // coarse grid => many ties and duplicates
                    foreach (var special in new[] { false, true })   // include NaN and +/- infinity
                        yield return [objectives, grid, special];
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Ranks_match_the_reference_implementation(int objectives, int grid, bool special)
        {
            var rnd = new Random(objectives * 1000 + grid + (special ? 1 : 0));
            double[] specials = [double.NaN, double.PositiveInfinity, double.NegativeInfinity];

            for (var trial = 0; trial < 200; trial++)
            {
                var n = rnd.Next(1, 120);
                var population = Enumerable.Range(0, n).Select(i =>
                    new Individual(i).AddFitnessValues([.. Enumerable.Range(0, objectives).Select(_ =>
                        special && rnd.Next(10) == 0 ? specials[rnd.Next(specials.Length)] : rnd.Next(grid) - grid / 2.0)])
                ).ToList();

                var expected = ReferenceRanks(population);
                var actual = FastNonDominatedSort.AssignParetoRanks(population);

                Assert.Equal(n, actual.Count);
                for (var i = 0; i < n; i++)
                {
                    if (expected[i] is null)
                    {
                        // NaN can make "dominance" cyclic; such individuals are never ranked (null entry).
                        Assert.Null(actual[i]);
                        continue;
                    }
                    Assert.Equal(population[i].Genes[0], actual[i].Genes[0]);              // order preserved
                    Assert.Equal(population[i].FitnessValues, actual[i].FitnessValues);
                    Assert.Equal(expected[i], actual[i].ParetoRank);
                }
            }
        }

        [Fact]
        public void Empty_population_gives_empty_result()
            => Assert.Empty(FastNonDominatedSort.AssignParetoRanks(new List<EvaluatedIndividual>()));

        /// <summary>
        /// The original O(M N^2) implementation (Deb et al. 2002), used as the oracle.
        /// Returns null for individuals that are never assigned a front (possible with NaN).
        /// </summary>
        private static int?[] ReferenceRanks(IList<EvaluatedIndividual> population)
        {
            var n = population.Count;
            var dominationCounts = new int[n];
            var dominatedSolutions = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
            var currentFront = new List<int>();

            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (population[i].ParetoDominates(population[j])) dominatedSolutions[i].Add(j);
                    else if (population[j].ParetoDominates(population[i])) dominationCounts[i]++;
                }
                if (dominationCounts[i] == 0) currentFront.Add(i);
            }

            var ranks = new int?[n];
            for (var rank = 0; currentFront.Count > 0; rank++)
            {
                var nextFront = new List<int>();
                foreach (var i in currentFront)
                {
                    ranks[i] = rank;
                    foreach (var j in dominatedSolutions[i])
                        if (--dominationCounts[j] == 0) nextFront.Add(j);
                }
                currentFront = nextFront;
            }
            return ranks;
        }
    }
}
