using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace EvolutionaryAlgorithm
{
    public static class EvalutatedIndividualExtensions
    {
        public static double Distance(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
        {
            // Optimized: avoid LINQ allocation and use squared distance when possible
            var sum = 0.0;
            for (int i = 0; i < @this.FitnessValues.Count; i++)
            {
                var diff = @this.FitnessValues[i] - other.FitnessValues[i];
                sum += diff * diff; // Faster than Math.Pow(diff, 2.0)
            }
            return Math.Sqrt(sum);
        }
        
        public static double SquaredDistance(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
        {
            // Even faster when actual distance isn't needed (e.g., for comparisons)
            var sum = 0.0;
            for (int i = 0; i < @this.FitnessValues.Count; i++)
            {
                var diff = @this.FitnessValues[i] - other.FitnessValues[i];
                sum += diff * diff;
            }
            return sum;
        }

        public static bool IsParetoDominatedBy(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
            => other.ParetoDominates(@this);

        public static bool ParetoDominates(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
        {
            // Optimized version with early exit and no allocations
            var hasBetter = false;
            var fitnessCount = @this.FitnessValues.Count;
            
            for (int i = 0; i < fitnessCount; i++)
            {
                var diff = @this.FitnessValues[i] - other.FitnessValues[i];
                
                if (diff > 0) // @this is worse in this objective
                    return false; // Cannot dominate if worse in any objective
                    
                if (diff < 0) // @this is better in this objective
                    hasBetter = true;
            }
            
            return hasBetter; // Dominates only if better in at least one and not worse in any
        }

    }
}
