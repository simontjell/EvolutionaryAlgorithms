using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace CSharpDE
{
    public class Offspring : Individual
    {
        public ImmutableList<EvaluatedIndividual> Parents { get; private set; }

        public Offspring(EvaluatedIndividual parent, params double[] genes) : this(new List<EvaluatedIndividual> { parent }.ToImmutableList(), genes) { }

        public Offspring(EvaluatedIndividual parent, Individual individual) : this(parent, individual.Genes.ToArray()) { }

        public Offspring(ImmutableList<EvaluatedIndividual> parents, params double[] genes) : base(genes)
        {
            Parents = parents;
        }

    }

    public class EvaluatedOffspring : Offspring
    {
        public ImmutableList<double> FitnessValues { get; private set; }

        public EvaluatedOffspring(Offspring offspring, ImmutableList<double> fitnessValues) : base(offspring.Parents, offspring.Genes.ToArray())
        {
            FitnessValues = fitnessValues;
        }
    }
}