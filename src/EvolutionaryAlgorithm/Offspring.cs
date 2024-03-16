using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace EvolutionaryAlgorithm
{
    public class Offspring(ImmutableList<EvaluatedIndividual> parents, params double[] genes) : Individual(genes)
    {
        public ImmutableList<EvaluatedIndividual> Parents { get; } = parents;

        public Offspring(EvaluatedIndividual parent, params double[] genes) : this([parent], genes) { }

        public Offspring(EvaluatedIndividual parent, Individual individual) : this(parent, [.. individual.Genes]) { }
    }
}