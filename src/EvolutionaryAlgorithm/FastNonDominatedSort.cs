using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    /// <summary>
    /// Fast Non-Dominated Sorting implementation based on NSGA-II algorithm
    /// Reduces complexity from O(n³) to O(n²) for Pareto ranking
    ///
    /// Reference:
    /// Deb, K., Pratap, A., Agarwal, S., & Meyarivan, T. (2002).
    /// A fast and elitist multiobjective genetic algorithm: NSGA-II.
    /// IEEE transactions on evolutionary computation, 6(2), 182-197.
    /// DOI: 10.1109/4235.996017
    /// </summary>
    public static class FastNonDominatedSort
    {
        public static IImmutableList<ParetoEvaluatedIndividual> AssignParetoRanks(IList<EvaluatedIndividual> population)
        {
            var n = population.Count;
            if (n == 0)
                return ImmutableList<ParetoEvaluatedIndividual>.Empty;

            var numObjectives = population[0].FitnessValues.Count;

            // Copy the fitness values into one flat array: indexing an ImmutableList<double> is an
            // O(log n) tree walk, and the domination loop below reads every value O(n) times.
            var fitness = new double[n * numObjectives];
            for (int i = 0; i < n; i++)
            {
                population[i].FitnessValues.CopyTo(fitness, i * numObjectives);
            }

            var ranks = numObjectives == 1
                ? RanksForSingleObjective(fitness)
                : RanksForMultipleObjectives(fitness, n, numObjectives);

            var result = new ParetoEvaluatedIndividual[n];
            for (int i = 0; i < n; i++)
            {
                // Individuals that never get a front (only possible when NaN makes dominance cyclic) stay null
                if (ranks[i] >= 0)
                {
                    result[i] = population[i].AddParetoRank(ranks[i]);
                }
            }

            return result.ToImmutableList();
        }

        /// <summary>
        /// With one objective, "a dominates b" means a &lt; b, so the front index is the number of
        /// distinct smaller values (a dense rank). A sort gives it in O(n log n) instead of O(n²).
        /// NaN neither dominates nor is dominated, so NaN values are in the first front.
        /// </summary>
        private static int[] RanksForSingleObjective(double[] fitness)
        {
            var n = fitness.Length;
            var ranks = new int[n];
            var values = new double[n];
            var indices = new int[n];
            var count = 0;

            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(fitness[i]))
                    continue; // rank 0

                values[count] = fitness[i];
                indices[count] = i;
                count++;
            }

            Array.Sort(values, indices, 0, count);

            var rank = 0;
            for (int k = 0; k < count; k++)
            {
                // Same test as ParetoDominates: the previous value dominates this one if the difference is negative
                if (k > 0 && values[k - 1] - values[k] < 0)
                    rank++;

                ranks[indices[k]] = rank;
            }

            return ranks;
        }

        /// <summary>
        /// Deb's fast non-dominated sort, comparing each pair only once.
        /// Returns -1 for individuals that never reach a front.
        /// </summary>
        private static int[] RanksForMultipleObjectives(double[] fitness, int n, int m)
        {
            var dominationCounts = new int[n]; // How many individuals dominate this one
            var dominatedSolutions = new List<int>[n]; // Which individuals this one dominates

            for (int i = 0; i < n; i++)
            {
                dominatedSolutions[i] = new List<int>();
            }

            // Calculate domination relationships: one pass over the objectives per unordered pair
            for (int i = 0; i < n; i++)
            {
                var oi = i * m;

                for (int j = i + 1; j < n; j++)
                {
                    var oj = j * m;
                    var iBetter = false;
                    var jBetter = false;

                    for (int k = 0; k < m; k++)
                    {
                        var diff = fitness[oi + k] - fitness[oj + k];

                        if (diff < 0)
                            iBetter = true;
                        else if (diff > 0)
                            jBetter = true;
                    }

                    // Dominates = better in at least one objective and worse in none (NaN differences count as neither)
                    if (iBetter && !jBetter)
                    {
                        dominatedSolutions[i].Add(j);
                        dominationCounts[j]++;
                    }
                    else if (jBetter && !iBetter)
                    {
                        dominatedSolutions[j].Add(i);
                        dominationCounts[i]++;
                    }
                }
            }

            var ranks = new int[n];
            Array.Fill(ranks, -1);

            // If not dominated by anyone, belongs to first front
            var currentFront = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (dominationCounts[i] == 0)
                {
                    currentFront.Add(i);
                }
            }

            // Build fronts
            for (int rank = 0; currentFront.Count > 0; rank++)
            {
                var nextFront = new List<int>();

                foreach (int i in currentFront)
                {
                    ranks[i] = rank;

                    foreach (int j in dominatedSolutions[i])
                    {
                        dominationCounts[j]--;
                        if (dominationCounts[j] == 0)
                        {
                            nextFront.Add(j);
                        }
                    }
                }

                currentFront = nextFront;
            }

            return ranks;
        }
    }
}
