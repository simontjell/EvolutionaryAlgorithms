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
    }
}
