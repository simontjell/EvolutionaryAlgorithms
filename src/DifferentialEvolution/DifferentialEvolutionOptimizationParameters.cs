using System;
using EvolutionaryAlgorithm.TerminationCriteria;
using EvolutionaryAlgorithm;

namespace DifferentialEvolution
{
    public class DifferentialEvolutionOptimizationParameters : OptimizationParameters
    {
        private const double DefaultCr = 0.5;
        private const double DefaultF = 1.0;
        public double CR { get; init; }
        public double F { get; init; }

        public DifferentialEvolutionOptimizationParameters(int populationSize, double cr = DefaultCr, double f = DefaultF, params ITerminationCriterion[] terminationCriteria) : base(populationSize, terminationCriteria)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(populationSize, 4);
            CR = cr;
            F = f;
        }

        public DifferentialEvolutionOptimizationParameters(int populationSize, params ITerminationCriterion[] terminationCriteria) : this(populationSize, DefaultCr, DefaultF, terminationCriteria){}
    }
}