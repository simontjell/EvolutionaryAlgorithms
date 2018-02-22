using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace CSharpDE
{
    public class Offspring : Individual
    {
        public ImmutableList<Individual> Parents { get; private set; }

        public Offspring(Individual parent, params double[] genes) : this(new List<Individual> { parent }.ToImmutableList(), genes) { }

        public Offspring(Individual parent, Individual individual) : this(parent, individual.Genes.ToArray()) { }

        public Offspring(ImmutableList<Individual> parents, params double[] genes) : base(genes)
        {
            Parents = parents;
        }

    }
}