using System.Linq;
using System.Collections.Immutable;

namespace CSharpDE
{
    public class NaiveOptimizationAlgorithm : EvolutionaryAlgorithm<OptimizationParameters>
    {
        public NaiveOptimizationAlgorithm(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters){ }

        protected override ImmutableList<Offspring> CreateOffspring(ImmutableList<EvaluatedIndividual> parents)
            => parents
               .Select(p => new Offspring(p, _optimizationProblem.CreateRandomIndividual()))
               .ToImmutableList();

        protected override ImmutableList<EvaluatedIndividual> SelectParents()
            => Generations.Last().Population;
    }
}