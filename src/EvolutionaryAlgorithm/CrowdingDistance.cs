using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    /// <summary>
    /// Efficient crowding distance calculation for diversity preservation
    /// Replaces expensive ScatteringMeasure with O(n log n) algorithm
    /// </summary>
    public static class CrowdingDistance
    {
        public static Dictionary<ParetoEvaluatedIndividual, double> CalculateCrowdingDistances(
            IEnumerable<ParetoEvaluatedIndividual> population)
        {
            var populationList = population.ToList();
            var distances = new Dictionary<ParetoEvaluatedIndividual, double>();
            
            if (populationList.Count <= 2)
            {
                // Boundary individuals get infinite distance
                foreach (var ind in populationList)
                {
                    distances[ind] = double.MaxValue;
                }
                return distances;
            }
            
            // Initialize all distances to 0
            foreach (var ind in populationList)
            {
                distances[ind] = 0.0;
            }
            
            var objectiveCount = populationList[0].FitnessValues.Count;
            
            // Calculate crowding distance for each objective
            for (int m = 0; m < objectiveCount; m++)
            {
                // Sort by this objective
                var sorted = populationList.OrderBy(ind => ind.FitnessValues[m]).ToList();
                
                // Get range of this objective
                var minValue = sorted.First().FitnessValues[m];
                var maxValue = sorted.Last().FitnessValues[m];
                var range = maxValue - minValue;
                
                // Boundary points get infinite distance
                distances[sorted.First()] = double.MaxValue;
                distances[sorted.Last()] = double.MaxValue;
                
                // Skip if all values are the same
                if (range == 0) continue;
                
                // Calculate distance contribution from this objective
                for (int i = 1; i < sorted.Count - 1; i++)
                {
                    if (distances[sorted[i]] == double.MaxValue) continue;
                    
                    var distance = (sorted[i + 1].FitnessValues[m] - sorted[i - 1].FitnessValues[m]) / range;
                    distances[sorted[i]] += distance;
                }
            }
            
            return distances;
        }
        
        /// <summary>
        /// Calculate crowding distances only within specific Pareto ranks
        /// More efficient when dealing with ranked populations
        /// </summary>
        public static Dictionary<ParetoEvaluatedIndividual, double> CalculateCrowdingDistancesByRank(
            IEnumerable<ParetoEvaluatedIndividual> population)
        {
            var allDistances = new Dictionary<ParetoEvaluatedIndividual, double>();
            
            // Group by Pareto rank
            var rankGroups = population.GroupBy(ind => ind.ParetoRank);
            
            foreach (var group in rankGroups)
            {
                var rankDistances = CalculateCrowdingDistances(group.ToList());
                foreach (var kvp in rankDistances)
                {
                    allDistances[kvp.Key] = kvp.Value;
                }
            }
            
            return allDistances;
        }
    }
}