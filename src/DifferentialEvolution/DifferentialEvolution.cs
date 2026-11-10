using System;
using System.Linq;
using System.Collections.Immutable;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution
{
    // https://en.wikipedia.org/wiki/Differential_evolution
    public class DifferentialEvolution(IOptimizationProblem optimizationProblem, DifferentialEvolutionOptimizationParameters optimizationParameters, Random random, params Individual[] injectedIndividuals) : EvolutionaryAlgorithm<DifferentialEvolutionOptimizationParameters>(optimizationProblem, optimizationParameters, injectedIndividuals)
    {
        private readonly Random _random = random;

        protected override IImmutableList<ParetoEvaluatedIndividual> SelectParents()
            => Generations.Last().Population;   // Take all...

        protected override IImmutableList<Offspring> CreateOffspring(IImmutableList<ParetoEvaluatedIndividual> parents)
        {
            var population = parents.ToArray(); // O(1) indexing (ImmutableList indexing is O(log n))
            var n = population[0].Genes.Count;
            var offspring = ImmutableList.CreateBuilder<Offspring>();

            for (var i = 0; i < population.Length; i++)
            {
                var x = population[i];
                var (a, b, c) = SelectDonors(i, population);
                var R = _random.Next(0, n);

                offspring.Add(new Offspring(x, new Individual(x.Genes.Select((xi, j) => _random.NextDouble() < _optimizationParameters.CR || j == R ? a[j] + _optimizationParameters.F * (b[j] - c[j]) : xi).ToArray())));
            }

            return offspring.ToImmutable();
        }

        /// <summary>
        /// Picks three distinct donors a, b, c, all different from population[exclude].
        /// Draws a uniformly random ordered triple in O(1). This has the same distribution as
        /// shuffling all other individuals and taking the first three (which is O(n log n)).
        /// </summary>
        private (ParetoEvaluatedIndividual, ParetoEvaluatedIndividual, ParetoEvaluatedIndividual) SelectDonors(int exclude, ParetoEvaluatedIndividual[] population)
        {
            // Draw from a range shrunk by the number of excluded indices, then shift the result
            // past each excluded index (in ascending order) to map it onto the allowed indices.
            var count = population.Length;

            var a = _random.Next(count - 1);
            if (a >= exclude) a++;

            var (lo, hi) = exclude < a ? (exclude, a) : (a, exclude);
            var b = _random.Next(count - 2);
            if (b >= lo) b++;
            if (b >= hi) b++;

            // Sort the three excluded indices {lo, hi, b} (lo < hi already).
            var (e0, e1, e2) = b < lo ? (b, lo, hi) : b < hi ? (lo, b, hi) : (lo, hi, b);
            var c = _random.Next(count - 3);
            if (c >= e0) c++;
            if (c >= e1) c++;
            if (c >= e2) c++;

            return (population[a], population[b], population[c]);
        }
    }
}
