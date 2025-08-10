using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    /// <summary>
    /// Fast Non-Dominated Sorting implementation based on NSGA-II algorithm
    /// Reduces complexity from O(n³) to O(n²) for Pareto ranking
    /// </summary>
    public static class FastNonDominatedSort
    {
        public static IImmutableList<ParetoEvaluatedIndividual> AssignParetoRanks(IList<EvaluatedIndividual> population)
        {
            var n = population.Count;
            var dominationCounts = new int[n]; // How many individuals dominate this one
            var dominatedSolutions = new List<int>[n]; // Which individuals this one dominates
            
            // Initialize arrays
            for (int i = 0; i < n; i++)
            {
                dominatedSolutions[i] = new List<int>();
            }
            
            var fronts = new List<List<int>>();
            var currentFront = new List<int>();
            
            // Calculate domination relationships
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    
                    if (population[i].ParetoDominates(population[j]))
                    {
                        dominatedSolutions[i].Add(j);
                    }
                    else if (population[j].ParetoDominates(population[i]))
                    {
                        dominationCounts[i]++;
                    }
                }
                
                // If not dominated by anyone, belongs to first front
                if (dominationCounts[i] == 0)
                {
                    currentFront.Add(i);
                }
            }
            
            // Build fronts
            int rank = 0;
            while (currentFront.Count > 0)
            {
                fronts.Add(new List<int>(currentFront));
                var nextFront = new List<int>();
                
                foreach (int i in currentFront)
                {
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
                rank++;
            }
            
            // Assign ranks to individuals
            var result = new ParetoEvaluatedIndividual[n];
            for (int frontIndex = 0; frontIndex < fronts.Count; frontIndex++)
            {
                foreach (int individualIndex in fronts[frontIndex])
                {
                    result[individualIndex] = population[individualIndex].AddParetoRank(frontIndex);
                }
            }
            
            return result.ToImmutableList();
        }
    }
}