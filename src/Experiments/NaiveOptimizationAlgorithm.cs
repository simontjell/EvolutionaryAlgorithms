using System.Linq;
using System.Collections.Immutable;
using SimpleSystemer.EA;

namespace CSharpDE
{
    public class NaiveOptimizationAlgorithm : EvolutionaryAlgorithm<OptimizationParameters>
    {
        public NaiveOptimizationAlgorithm(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters){ }

        protected override ImmutableList<Offspring> CreateOffspring(ImmutableList<ParetoEvaluatedIndividual> parents)
            => parents
               .Select(p => new Offspring(p, _optimizationProblem.CreateRandomIndividual()))
               .ToImmutableList();

        protected override ImmutableList<ParetoEvaluatedIndividual> SelectParents()
            => Generations.Last().Population;
    }
}