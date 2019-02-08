using System.Linq;
using System.Collections.Immutable;
using SimpleSystemer.EA;

namespace CSharpDE
{
    public class NaiveOptimizationAlgorithm : EvolutionaryAlgorithm<OptimizationParameters>
    {
        public NaiveOptimizationAlgorithm(IOptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters){ }

        protected override IImmutableList<Offspring> CreateOffspring(IImmutableList<ParetoEvaluatedIndividual> parents)
            => parents
               .Select(p => new Offspring(p, _optimizationProblem.CreateRandomIndividual()))
               .ToImmutableList();

        protected override IImmutableList<ParetoEvaluatedIndividual> SelectParents()
            => Generations.Last().Population;
    }
}