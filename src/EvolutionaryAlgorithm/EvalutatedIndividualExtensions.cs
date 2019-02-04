using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CSharpDE
{
    public static class EvalutatedIndividualExtensions
    {
        public static double Distance(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
            => Math.Sqrt(@this.FitnessValues.Select((q, i) => Math.Pow(q - other.FitnessValues[i], 2.0)).Sum());

        public static bool IsParetoDominatedBy(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
            => other.ParetoDominates(@this);

        public static bool ParetoDominates(this IEvaluatedIndividual @this, IEvaluatedIndividual other)
        {
            var compared = @this.FitnessValues.Select(
                (fitnessValue, index) =>
                new
                {
                    SubjectBetter = fitnessValue < other.FitnessValues[index],
                    SubjectBetterOrEqual = fitnessValue <= other.FitnessValues[index],
                }
            );

            return compared.All(c => c.SubjectBetterOrEqual) && compared.Any(c => c.SubjectBetter);
        }

    }
}
