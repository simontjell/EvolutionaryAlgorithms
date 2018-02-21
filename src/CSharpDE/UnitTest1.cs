using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using System.Collections.Immutable;

namespace CSharpDE
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var optimizationAlgorithm = new DifferentialEvolution(
                new MyOptimizationProblem(),
                new DifferentialEvolutionOptimizationParameters(
                    100,
                    new LambdaTerminationCriterion(algorithm => algorithm.Generations.Last().Population.Any(individual => algorithm.FitnessValues[individual] == 0.0))  // TODO: Rethink the interface for getting best fit individual(s)
                )
            );

            optimizationAlgorithm.Optimize();

        }
    }

    public class NaiveOptimizationAlgorithm : EvolutionaryAlgorithm<OptimizationParameters>
    {
        public NaiveOptimizationAlgorithm(OptimizationProblem optimizationProblem, OptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters){ }

        protected override ImmutableList<Offspring> CreateOffspring(ImmutableList<Individual> parents)
            => parents
               .Select(p => new Offspring(p, _optimizationProblem.CreateRandomIndividual()))
               .ToImmutableList();

        protected override ImmutableList<Individual> SelectParents()
            => Generations.Last().Population;
    }

    public class LambdaTerminationCriterion : TerminationCriterion
    {
        private readonly Func<EvolutionaryAlgorithm, bool> _shouldTerminate;

        public LambdaTerminationCriterion(Func<EvolutionaryAlgorithm, bool> shouldTerminate)
        {
            _shouldTerminate = shouldTerminate;
        }

        public override bool ShouldTerminate(EvolutionaryAlgorithm optimizationAlgorithm) => _shouldTerminate(optimizationAlgorithm);
    }

    public class DifferentialEvolutionOptimizationParameters : OptimizationParameters
    {
        public double CR { get; private set; } = 0.5;
        public double F { get; private set; } = 1.0;

        public DifferentialEvolutionOptimizationParameters(int populationSize, params TerminationCriterion[] terminationCriteria) : base(populationSize, terminationCriteria)
        {
            if (populationSize < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(populationSize), "The population must consist of at least 4 individuals");
            }
        }
    }


    // https://en.wikipedia.org/wiki/Differential_evolution
    public class DifferentialEvolution : EvolutionaryAlgorithm<DifferentialEvolutionOptimizationParameters>
    {
        private readonly Random _random;

        // TODO: This are parameters - but DE-specific ones...

        public DifferentialEvolution(OptimizationProblem optimizationProblem, DifferentialEvolutionOptimizationParameters optimizationParameters) : base(optimizationProblem, optimizationParameters)
        {
            _random = new Random((int)DateTime.Now.Ticks);
        }

        protected override ImmutableList<Individual> SelectParents()
            => Generations.Last().Population;   // Take all...

        protected override ImmutableList<Offspring> CreateOffspring(ImmutableList<Individual> parents)
        {
            var n = parents.First().Genes.Count;

            return 
                parents
                .Select(x =>
                    {
                        var abc = parents.OrderBy(individual => individual == x ? 0.0 : 1.0 + _random.NextDouble()).Skip(1).Take(3).ToImmutableList();
                        var a = abc[0];
                        var b = abc[1];
                        var c = abc[2];

                        var R = _random.Next(0, n);

                return new Offspring(x, new Individual(x.Genes.Select((xi, i) => _random.NextDouble() < _optimizationParameters.CR || i == R ? a[i] + _optimizationParameters.F * (b[i] - c[i]) : xi).ToArray()));
                    }
                )
                .ToImmutableList();
        }
    }


    public class MyOptimizationProblem : OptimizationProblem
    {
        private readonly Random _rnd;

        public MyOptimizationProblem()
        {
            _rnd = new Random((int)DateTime.Now.Ticks);
        }
        
        public override bool IsFeasible(Individual individual) => true;
        public override double CalculateFitnessValue(Individual individual) => Math.Pow(individual[0] * individual[1], 2.0);
        public override Individual CreateRandomIndividual() => new Individual(_rnd.NextDouble(), _rnd.NextDouble());
    }

    public class Individual
    {
        public readonly ImmutableList<double> Genes;
        public double this[int index] => Genes[index];

        public Individual(params double[] genes)
        {
            Genes = genes.ToImmutableList();
        }
    }

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

    public class Generation
    {
        public Generation(ImmutableList<Individual> population)
        {
            Population = population;
        }

        public ImmutableList<Individual> Population { get; private set; }
    }
}