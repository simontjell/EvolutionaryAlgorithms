using CSharpDE;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Experiments
{
    class Program
    {
        static void Main(string[] args)
        {
            var optimizationAlgorithm = new DifferentialEvolution(
                new SphereOptimizationProblem(10),
                    new DifferentialEvolutionOptimizationParameters(
                        100,
                        new LambdaTerminationCriterion(algorithm => algorithm.Generations.Count >= 100000)  // TODO: Rethink the interface for getting best fit individual(s)
                    )
                );

            optimizationAlgorithm.OnGenerationFinished += OptimizationAlgorithm_OnGenerationFinished;

            optimizationAlgorithm.Optimize();
        }

        static ParetoEvaluatedIndividual _bestFitness = null;

        private static void OptimizationAlgorithm_OnGenerationFinished(object sender, EventArgs e)
        {
            var algo = (sender as DifferentialEvolution);
            var bestFitness = algo.GetBestIndividuals(algo.Generations.Last()).First();

            if (bestFitness != _bestFitness)
            {
                Console.WriteLine($"{algo.Generations.Count}: {bestFitness}");
                _bestFitness = bestFitness;
            }
            return;


            var generations = algo.Generations;
            //            var points = algo.GetBestIndividuals(generations.Last()).Select(i => new PlotPoint { X = i.FitnessValues[0], Y = i.FitnessValues[1], Value = i.ParetoRank }).Where(p => p.Value < 10);

            var points = algo.Generations.Select((g, i) => new PlotPoint { X = (double)i, Y = algo.GetBestIndividuals(g).Single().FitnessValues.Single(), Value = 1 });

            int width = 80, height = 25;
            var pixels = Render(points, width, height);

            Print(pixels, width, height);
        }

        private static void Print(int?[] pixels, int width, int height)
        {
            Console.Clear();

            for (var i = 0; i < width * height; i += width)
            {
                var line = string.Join(string.Empty, pixels.Skip(i).Take(width).Select(p => p.HasValue ? p.ToString() : "."));
                Console.WriteLine(line);
            }

            Thread.Sleep(2000);
        }

        private static int?[] Render(IEnumerable<PlotPoint> points, int width, int height)
        {
            double maxX = points.Max(p => p.X);
            double minX = points.Min(p => p.X);
            double maxY = points.Max(p => p.Y);
            double minY = points.Min(p => p.Y);

            var pixels = new int?[width * height];
            foreach (var point in points)
            {
                int x = (int)(((((double)point.X - minX)) / (maxX - minX)) * (double)width + 0.5);
                int y = (int)(((((double)point.Y - minY)) / (maxY - minY)) * (double)height + 0.5);

                pixels[x * y] = point.Value + 1;
            }

            return pixels;
        }

        // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_single-objective_optimization
        public class SphereOptimizationProblem : OptimizationProblem
        {
            private readonly Random _rnd;
            private readonly int _n;

            public SphereOptimizationProblem(int n)
            {
                _rnd = new Random((int)DateTime.Now.Ticks);
                _n = n;
            }

            public override bool IsFeasible(Individual individual) => true;
            public override ImmutableList<double> CalculateFitnessValue(Individual individual) => new List<double> { individual.Genes.Sum(g => Math.Pow(g, 2.0)) }.ToImmutableList();
            public override Individual CreateRandomIndividual() => new Individual(Enumerable.Range(0, _n).Select(i => _rnd.NextDouble()).ToArray());
        }

        // https://en.wikipedia.org/wiki/Test_functions_for_optimization#Test_functions_for_multi-objective_optimization
        public class SchafferFunctionOptimizationProblem : OptimizationProblem
        {
            private readonly Random _rnd;

            public SchafferFunctionOptimizationProblem()
            {
                _rnd = new Random((int)DateTime.Now.Ticks);
            }

            public override ImmutableList<double> CalculateFitnessValue(Individual individual)
            {
                return new List<double> { 
                    Math.Pow(individual[0], 2.0),
                    Math.Pow(individual[0] - 2, 2.0)
                }.ToImmutableList();
            }

            public override Individual CreateRandomIndividual()
            {
                return new Individual(_rnd.NextDouble() * 10.0, _rnd.NextDouble() * 10.0);
            }
        }

        private class PlotPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public int Value { get; set; }

            public override string ToString() => $"({X} ; {Y})";
        }
    }
}
