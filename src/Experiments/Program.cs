using SimpleSystemer.EA.DE;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Drawing;
using SimpleSystemer.EA;
using Experiments.OptimizationProblems;
using SimpleSystemer.EA.TerminationCriteria;

namespace Experiments
{

    public class Program
    {
        

        static void Main(string[] args)
        {
            var period =
                new Period(
                    new List<Observation> {
                        new Observation(new DateTime(1977,1,1).AddHours(1), 100.0),
                        new Observation(new DateTime(1977,1,1).AddHours(2), 120.0),
                        new Observation(new DateTime(1977,1,1).AddHours(3), 200.0),
                        new Observation(new DateTime(1977,1,2).AddHours(1), 2100.0),
                        new Observation(new DateTime(1977,1,2).AddHours(2), 2120.0),
                        new Observation(new DateTime(1977,1,2).AddHours(3), 2200.0),
                        new Observation(new DateTime(1977,1,2).AddHours(4), 22.0),
                    }
                );



            var value = period.Days[0].NormalizedObservations[0.75];

            var optimizationAlgorithm = new DifferentialEvolution(
                new BoothOptimizationProblem(),
                //new SchafferFunctionOptimizationProblem(),
                new DifferentialEvolutionOptimizationParameters(
                    100,
                    new GenerationCountTerminationCriterion(5000)
                )
            );

            //optimizationAlgorithm.OnGenerationFinished += (sender, eventArgs) => OptimizationAlgorithm_OnGenerationFinished(sender, eventArgs, true);
            optimizationAlgorithm.OnGenerationFinished += PrintBestFitness;

            optimizationAlgorithm.Optimize();
        }

        static ParetoEvaluatedIndividual _bestFitness = null;


        private static void PrintBestFitness(object sender, EventArgs e)
        {
            var algo = (sender as DifferentialEvolution);
            var bestFitness = algo.GetBestIndividuals(algo.Generations.Last()).First();

            if (bestFitness != _bestFitness)
            {
                Console.WriteLine($"{algo.Generations.Count}: {bestFitness}");
                _bestFitness = bestFitness;
            }
        }

        private static void OptimizationAlgorithm_OnGenerationFinished(object sender, EventArgs e, bool isMultiObjective)
        {
            var algo = (sender as DifferentialEvolution);

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



        private class PlotPoint
        {
            public decimal X { get; set; }
            public decimal Y { get; set; }
            public Color Color { get; set; }

            public override string ToString() => $"({X} ; {Y})";
        }

    }
}
