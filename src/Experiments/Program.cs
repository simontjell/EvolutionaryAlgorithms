using CSharpDE;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Drawing;

namespace Experiments
{
    class Program
    {
        static void Main(string[] args)
        {
            var optimizationAlgorithm = new DifferentialEvolution(
                //new SphereOptimizationProblem(10),
                new SchafferFunctionOptimizationProblem(),
                    new DifferentialEvolutionOptimizationParameters(
                        100,
                        new LambdaTerminationCriterion(algorithm => algorithm.Generations.Count >= 100000)  // TODO: Rethink the interface for getting best fit individual(s)
                    )
                );

            optimizationAlgorithm.OnGenerationFinished += (sender, eventArgs) => OptimizationAlgorithm_OnGenerationFinished(sender, eventArgs, true);

            optimizationAlgorithm.Optimize();
        }

        static ParetoEvaluatedIndividual _bestFitness = null;

        private static void OptimizationAlgorithm_OnGenerationFinished(object sender, EventArgs e, bool isMultiObjective)
        {
            var algo = (sender as DifferentialEvolution);
            var bestFitness = algo.GetBestIndividuals(algo.Generations.Last()).First();

            if (bestFitness != _bestFitness)
            {
                Console.WriteLine($"{algo.Generations.Count}: {bestFitness}");
                _bestFitness = bestFitness;
            }

            var generations = algo.Generations;
            var points = 
                isMultiObjective ? 
                    (
                        Enumerable.Range(0, 1)
                        .SelectMany(
                            rank => 
                            algo.Generations.Last().Population
                            .Where(i => i.ParetoRank == rank)
                            .Select(i => new PlotPoint { X = (decimal)i.FitnessValues[0], Y = (decimal)i.FitnessValues[1], Color = GetRankColor(i.ParetoRank) })
                        )
                    )
                    :
                    algo.Generations.Select((g, i) => new PlotPoint { X = (decimal)i, Y = (decimal)algo.GetBestIndividuals(g).Single().FitnessValues.Single(), Color = Color.Yellow });

            int width = 200, height = 200;
            var pixels = Render(points, width, height);

            Print(pixels, width, height).Save(@"bin\Debug\netcoreapp2.0\status.jpg");
        }

        private static Color GetRankColor(int paretoRank)
        {
            switch (paretoRank)
            {
                case 0: return Color.Green;
                case 1: return Color.Yellow;
                case 2: return Color.Blue;
                case 3: return Color.Orange;
                case 4: return Color.Red;

                default:
                    return Color.White;
            }
        }

        private static Bitmap Print(IEnumerable<PlotPoint> points, int width, int height)
            => Print(Render(points, width, height), width, height);

        private static Bitmap Print(Color?[,] pixels, int width, int height)
        {
            var bitmap = new Bitmap(width, height);

            for (var x = 0; x < width; x++)
            {
                for(var y = 0; y < height; y++)
                {
                    if (pixels[x, y].HasValue)
                    {
                        bitmap.SetPixel(x, y, pixels[x, y].Value);
                    }
                }
            }

            return bitmap;
        }

        private static Color?[,] Render(IEnumerable<PlotPoint> points, int width, int height)
        {
            decimal maxX = points.Max(p => p.X);
            decimal minX = points.Min(p => p.X);
            decimal maxY = points.Max(p => p.Y);
            decimal minY = points.Min(p => p.Y);

            var pixels = new Color?[width,height];
            foreach (var point in points)
            {
                if (maxX - minX == 0 || maxY - minY == 0)
                {
                    continue;
                }

                int x = (int)(((((decimal)point.X - minX)) / (maxX - minX)) * (decimal)(width - 1));
                int y = height - 1 - (int)(((((decimal)point.Y - minY)) / (maxY - minY)) * (decimal)(height - 1));

                pixels[x,y] = point.Color;
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
            public decimal X { get; set; }
            public decimal Y { get; set; }
            public Color Color { get; set; }

            public override string ToString() => $"({X} ; {Y})";
        }

    }
}
