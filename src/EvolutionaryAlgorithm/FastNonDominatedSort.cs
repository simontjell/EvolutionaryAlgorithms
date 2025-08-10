using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    /// <summary>
    /// Fast non-dominated sorting algorithm based on NSGA-II
    /// Reduces complexity from O(n³) to O(n²) for Pareto ranking
    /// </summary>
    public static class FastNonDominatedSort
    {
        public static ImmutableList<ParetoEvaluatedIndividual> AssignParetoRanks(IList<EvaluatedIndividual> population)
        {
            var n = population.Count;
            var dominationCounts = new int[n]; // Number of individuals that dominate individual i
            var dominatedSets = new List<List<int>>(n); // Set of individuals dominated by individual i
            var fronts = new List<List<int>>();
            
            // Initialize dominated sets
            for (int i = 0; i < n; i++)
            {
                dominatedSets.Add(new List<int>());
            }
            
            // First front (rank 0) - non-dominated individuals
            var currentFront = new List<int>();
            
            // Compare all pairs once - O(n²)
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var dominance = CompareDominance(population[i], population[j]);
                    
                    if (dominance == 1) // i dominates j
                    {
                        dominatedSets[i].Add(j);
                        dominationCounts[j]++;
                    }
                    else if (dominance == -1) // j dominates i
                    {
                        dominatedSets[j].Add(i);
                        dominationCounts[i]++;
                    }
                    // dominance == 0 means neither dominates the other
                }
                
                // If no one dominates i, it belongs to the first front
                if (dominationCounts[i] == 0)
                {
                    currentFront.Add(i);
                }
            }
            
            // Assign ranks using fronts
            var result = new ParetoEvaluatedIndividual[n];
            var rank = 0;
            
            while (currentFront.Count > 0)
            {
                fronts.Add(currentFront);
                var nextFront = new List<int>();
                
                // Assign current rank to all individuals in current front
                foreach (var i in currentFront)
                {
                    result[i] = population[i].AddParetoRank(rank);
                    
                    // Reduce domination count for dominated individuals
                    foreach (var dominated in dominatedSets[i])
                    {
                        dominationCounts[dominated]--;
                        if (dominationCounts[dominated] == 0)
                        {
                            nextFront.Add(dominated);
                        }
                    }
                }
                
                currentFront = nextFront;
                rank++;
            }
            
            return result.ToImmutableList();
        }
        
        /// <summary>
        /// Compare dominance between two individuals
        /// Returns: 1 if a dominates b, -1 if b dominates a, 0 if neither dominates
        /// </summary>
        private static int CompareDominance(IEvaluatedIndividual a, IEvaluatedIndividual b)
        {
            var betterInAny = false;
            var worseInAny = false;
            
            for (int i = 0; i < a.FitnessValues.Count; i++)
            {
                var diff = a.FitnessValues[i] - b.FitnessValues[i];
                
                if (diff < 0) // a is better in this objective (minimization)
                    betterInAny = true;
                else if (diff > 0) // a is worse in this objective
                    worseInAny = true;
                    
                // Early exit if both better and worse
                if (betterInAny && worseInAny)
                    return 0; // Neither dominates
            }
            
            if (betterInAny && !worseInAny)
                return 1; // a dominates b
            else if (worseInAny && !betterInAny)
                return -1; // b dominates a
            else
                return 0; // Neither dominates (equal or incomparable)
        }
    }
}