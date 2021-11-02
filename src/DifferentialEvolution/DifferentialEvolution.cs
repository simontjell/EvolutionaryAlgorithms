using System;
using System.Linq;
using System.Collections.Immutable;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution
{
    // https://en.wikipedia.org/wiki/Differential_evolution
    public class DifferentialEvolution : EvolutionaryAlgorithm<DifferentialEvolutionOptimizationParameters>
    {
        private readonly Random _random;

        public DifferentialEvolution(IOptimizationProblem optimizationProblem, DifferentialEvolutionOptimizationParameters optimizationParameters, Random random, params Individual[] injectedIndividuals) : base(optimizationProblem, optimizationParameters, injectedIndividuals)
        {
            _random = random;
        }

        protected override IImmutableList<ParetoEvaluatedIndividual> SelectParents()
            => Generations.Last().Population;   // Take all...

        protected override IImmutableList<Offspring> CreateOffspring(IImmutableList<ParetoEvaluatedIndividual> parents)
        {
            var n = parents.First().Genes.Count;

            return 
                parents
                .Select(x =>
                    {
                        var (a, b, c) = 
                            parents
                            .Where(individual => individual != x)
                            .OrderBy(_ => _random.NextDouble())
                            .TakeTriplet()
                        ;

                        var R = _random.Next(0, n);

                        return new Offspring(x, new Individual(x.Genes.Select((xi, i) => _random.NextDouble() < _optimizationParameters.CR || i == R ? a[i] + _optimizationParameters.F * (b[i] - c[i]) : xi).ToArray()));
                    }
                )
                .ToImmutableList();
        }
    }
}