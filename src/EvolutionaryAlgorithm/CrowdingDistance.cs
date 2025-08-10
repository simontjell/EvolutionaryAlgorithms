using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    /// <summary>
    /// Crowding Distance calculation for maintaining diversity in Pareto fronts
    /// Used in NSGA-II algorithm for selecting individuals within the same rank
    /// </summary>
    public static class CrowdingDistance
    {
        public static Dictionary<ParetoEvaluatedIndividual, double> CalculateCrowdingDistances(IList<ParetoEvaluatedIndividual> individuals)
        {
            var distances = individuals.ToDictionary(ind => ind, ind => 0.0);
            var n = individuals.Count;
            
            if (n <= 2)
            {
                // If 2 or fewer individuals, give them infinite distance (they're all boundary points)
                foreach (var ind in individuals)
                {
                    distances[ind] = double.PositiveInfinity;
                }
                return distances;
            }
            
            var numObjectives = individuals.First().FitnessValues.Count;
            
            // Calculate crowding distance for each objective
            for (int objIndex = 0; objIndex < numObjectives; objIndex++)
            {
                // Sort individuals by this objective
                var sortedByObjective = individuals
                    .OrderBy(ind => ind.FitnessValues[objIndex])
                    .ToList();
                
                // Get objective range
                var minValue = sortedByObjective.First().FitnessValues[objIndex];
                var maxValue = sortedByObjective.Last().FitnessValues[objIndex];
                var range = maxValue - minValue;
                
                // Boundary points get infinite distance
                distances[sortedByObjective.First()] = double.PositiveInfinity;
                distances[sortedByObjective.Last()] = double.PositiveInfinity;
                
                // Calculate distance for intermediate points
                if (range > 0) // Avoid division by zero
                {
                    for (int i = 1; i < n - 1; i++)
                    {
                        if (!double.IsPositiveInfinity(distances[sortedByObjective[i]]))
                        {
                            var distance = (sortedByObjective[i + 1].FitnessValues[objIndex] - 
                                          sortedByObjective[i - 1].FitnessValues[objIndex]) / range;
                            distances[sortedByObjective[i]] += distance;
                        }
                    }
                }
            }
            
            return distances;
        }
    }
}