using System;
using System.Linq;
using System.Threading;
using Spectre.Console;
using DifferentialEvolution;

namespace EvolutionaryAlgorithms.CLI.UI;

/// <summary>
/// Rich UI components for displaying optimization progress
/// </summary>
public static class OptimizationUI
{
    public static void ShowLiveProgress(DifferentialEvolution.DifferentialEvolution algorithm)
    {
        // TODO: Implement rich live UI with charts and multiple panels
        // For now, just show basic progress
        
        AnsiConsole.Live(new Panel("Starting optimization..."))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(ctx =>
            {
                var progressTable = new Table();
                progressTable.AddColumn("Generation");
                progressTable.AddColumn("Best Fitness");
                progressTable.AddColumn("Avg Fitness");
                progressTable.AddColumn("Population Diversity");

                algorithm.OnGenerationFinished += (sender, args) =>
                {
                    var generation = algorithm.Generations.Last();
                    var bestIndividuals = algorithm.GetBestIndividuals(generation);
                    var bestFitness = bestIndividuals.First().FitnessValues.First();
                    
                    var allFitness = generation.Population.Select(p => p.FitnessValues.First()).ToList();
                    var avgFitness = allFitness.Average();
                    var diversity = CalculateDiversity(generation.Population);

                    progressTable.AddRow(
                        algorithm.Generations.Count.ToString(),
                        bestFitness.ToString("F6"),
                        avgFitness.ToString("F6"),
                        diversity.ToString("F4")
                    );

                    var panel = new Panel(progressTable)
                        .Header($"[bold green]Optimization Progress - {algorithm.GetType().Name}[/]")
                        .Border(BoxBorder.Rounded);
                    
                    ctx.UpdateTarget(panel);
                    
                    Thread.Sleep(50); // Small delay to make progress visible
                };
            });
    }

    private static double CalculateDiversity(System.Collections.Immutable.IImmutableList<EvolutionaryAlgorithm.ParetoEvaluatedIndividual> population)
    {
        // Simple diversity measure: standard deviation of fitness values
        if (population.Count < 2) return 0;
        
        var fitnessValues = population.Select(p => p.FitnessValues.First()).ToList();
        var mean = fitnessValues.Average();
        var variance = fitnessValues.Select(f => Math.Pow(f - mean, 2)).Average();
        return Math.Sqrt(variance);
    }
}