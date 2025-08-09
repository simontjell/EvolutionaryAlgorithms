using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using EvolutionaryAlgorithm.TerminationCriteria;
using DifferentialEvolution;
using EvolutionaryAlgorithms.CLI.UI;

namespace EvolutionaryAlgorithms.CLI.Commands;

/// <summary>
/// Generic command that executes optimization problems via reflection
/// </summary>
/// <typeparam name="TOptimizationProblem">The optimization problem type</typeparam>
/// <typeparam name="TSettings">The settings type</typeparam>
public class OptimizationProblemCommand<TOptimizationProblem, TSettings> : Command<TSettings>
    where TOptimizationProblem : class, IOptimizationProblemCommand<TSettings>
    where TSettings : CommandSettings
{
    public override int Execute(CommandContext context, TSettings settings)
    {
        try
        {
            // Get static methods via reflection
            var createProblemMethod = typeof(TOptimizationProblem)
                .GetMethod("CreateOptimizationProblem", BindingFlags.Static | BindingFlags.Public);
            var getParamsMethod = typeof(TOptimizationProblem)
                .GetMethod("GetAlgorithmParameters", BindingFlags.Static | BindingFlags.Public);

            if (createProblemMethod == null || getParamsMethod == null)
            {
                AnsiConsole.MarkupLine("[red]Required static methods not found on optimization problem type[/]");
                return 1;
            }

            var random = CreateRandom((settings as OptimizationSettingsBase)?.Seed);
            
            var problem = createProblemMethod.Invoke(null, new object[] { settings, random }) as IOptimizationProblem;
            var algorithmParams = getParamsMethod.Invoke(null, new object[] { settings }) as AlgorithmParameters;

            if (problem == null || algorithmParams == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to create optimization problem or parameters[/]");
                return 1;
            }
            
            RunOptimization(problem, algorithmParams, random);
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private static Random CreateRandom(int? seed)
    {
        var actualSeed = seed ?? Environment.TickCount;
        AnsiConsole.MarkupLine($"[dim]Using random seed: {actualSeed}[/]");
        return new Random(actualSeed);
    }

    private static void RunOptimization(IOptimizationProblem problem, AlgorithmParameters algorithmParams, Random random)
    {
        var parameters = new DifferentialEvolutionOptimizationParameters(
            algorithmParams.PopulationSize,
            algorithmParams.CrossoverRate,
            algorithmParams.DifferentialWeight,
            new GenerationCountTerminationCriterion(algorithmParams.MaxGenerations)
        );

        var algorithm = new DifferentialEvolution.DifferentialEvolution(problem, parameters, random);

        if (algorithmParams.UseUI)
        {
            // Use Spectre.Console panels but without interactive input
            RunOptimizationWithPanelUI(algorithm, problem, algorithmParams);
        }
        else
        {
            // Use standard console output
            RunOptimizationConsole(algorithm, problem, algorithmParams);
        }
    }

    private static async Task RunOptimizationWithUI(DifferentialEvolution.DifferentialEvolution algorithm, IOptimizationProblem problem, AlgorithmParameters algorithmParams)
    {
        var problemName = problem.GetType().Name;
        using var uiManager = new UIManager(problemName, algorithmParams);
        
        // Set up event handler to update UI
        algorithm.OnGenerationFinished += (sender, args) =>
        {
            var generation = algorithm.Generations.Last();
            var bestIndividuals = algorithm.GetBestIndividuals(generation);
            var bestFitness = bestIndividuals.First().FitnessValues.First();
            
            // Calculate population statistics
            var allFitnesses = generation.Population.Select(p => p.FitnessValues.First()).ToArray();
            var meanFitness = allFitnesses.Average();
            var stdFitness = allFitnesses.Length > 1 ? 
                Math.Sqrt(allFitnesses.Sum(f => Math.Pow(f - meanFitness, 2)) / allFitnesses.Length) : 0;
            
            uiManager.UpdateGeneration(
                algorithm.Generations.Count, 
                bestFitness, 
                meanFitness, 
                stdFitness,
                generation.Population
            );
            
            // Wait while paused or stopped - this blocks the generation loop
            while ((uiManager.IsPaused || uiManager.IsStopped) && !uiManager.ShouldExit)
            {
                Task.Delay(100).Wait();
            }
            
            // Check if user requested restart (rewind)
            if (uiManager.ShouldRestart)
            {
                // Stop current optimization - user can restart manually
                return; // This will exit the generation loop
            }
            
            // Add small delay between generations to make progress visible
            if (!uiManager.ShouldExit)
            {
                Task.Delay(50).Wait();
            }
        };
        
        // Start UI in background task
        var uiTask = Task.Run(() => uiManager.StartAsync());
        
        // Run optimization in separate task
        var optimizationTask = Task.Run(() =>
        {
            try
            {
                // Give UI time to start up
                Task.Delay(500).Wait();
                
                algorithm.Optimize(); // Run complete optimization - events handle pausing
                uiManager.SetOptimizationComplete();
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
            }
        });
        
        // Wait for either task to complete or user to quit
        await Task.WhenAny(uiTask, optimizationTask);
        
        // If optimization completed, keep UI running until user exits
        if (optimizationTask.IsCompleted && !uiManager.ShouldExit)
        {
            // Wait for user to quit manually with 'q'
            while (!uiManager.ShouldExit)
            {
                await Task.Delay(100);
            }
        }
        
        // Display final results if UI was exited
        if (uiManager.ShouldExit)
        {
            DisplayResults(algorithm, algorithmParams);
        }
    }

    private static void RunOptimizationWithPanelUI(DifferentialEvolution.DifferentialEvolution algorithm, IOptimizationProblem problem, AlgorithmParameters algorithmParams)
    {
        var problemName = problem.GetType().Name;
        var currentGeneration = 0;
        var bestFitness = double.MaxValue;
        var meanFitness = 0.0;
        var stdFitness = 0.0;
        var fitnessHistory = new List<double>();
        var status = "STARTING";
        
        // Create initial layout
        var layout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, status);
        
        // Use Live display to avoid flickering
        AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(ctx =>
            {
                // Set up event handler for real-time updates
                algorithm.OnGenerationFinished += (sender, args) =>
                {
                    var generation = algorithm.Generations.Last();
                    var bestIndividuals = algorithm.GetBestIndividuals(generation);
                    var newBestFitness = bestIndividuals.First().FitnessValues.First();
                    
                    // Calculate population statistics
                    var allFitnesses = generation.Population.Select(p => p.FitnessValues.First()).ToArray();
                    meanFitness = allFitnesses.Average();
                    stdFitness = allFitnesses.Length > 1 ? 
                        Math.Sqrt(allFitnesses.Sum(f => Math.Pow(f - meanFitness, 2)) / allFitnesses.Length) : 0;
                    
                    currentGeneration = algorithm.Generations.Count;
                    
                    // Only add to fitness history if fitness improved (for single-objective optimization)
                    // For minimization problems, improvement means lower fitness value
                    var isFirstGeneration = fitnessHistory.Count == 0;
                    var hasImproved = isFirstGeneration || newBestFitness < bestFitness;
                    
                    if (hasImproved)
                    {
                        bestFitness = newBestFitness;
                        fitnessHistory.Add(bestFitness);
                        if (fitnessHistory.Count > 20) fitnessHistory.RemoveAt(0); // Keep last 20
                    }
                    
                    status = "RUNNING";
                    
                    // Update the layout without flickering
                    var updatedLayout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, status);
                    ctx.UpdateTarget(updatedLayout);
                };

                algorithm.Optimize();
                
                // Final update
                status = "COMPLETED";
                var finalLayout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, status);
                ctx.UpdateTarget(finalLayout);
                
                // Hold the display briefly to show completion
                Thread.Sleep(1000);
            });
        
        DisplayResults(algorithm, algorithmParams);
    }
    
    private static Layout CreatePanelLayout(string problemName, AlgorithmParameters parameters, int generation, double bestFitness, double meanFitness, double stdFitness, List<double> fitnessHistory, string status)
    {
        // Create main layout
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(4),
                new Layout("Content"),
                new Layout("Footer").Size(3)
            );

        // Header Panel
        var headerContent = $"🧬 Differential Evolution - {problemName}\n" +
                           $"Generation: {generation}/{parameters.MaxGenerations}  |  Best Fitness: {bestFitness:F6}\n" +
                           $"Population: {parameters.PopulationSize}  |  Status: {status}";
        
        layout["Header"].Update(
            new Panel(headerContent)
                .Header("OPTIMIZATION DASHBOARD")
                .BorderColor(status == "COMPLETED" ? Color.Green : status == "RUNNING" ? Color.Blue : Color.Yellow)
        );

        // Content - split into left and right
        layout["Content"].SplitColumns(
            new Layout("Chart"),
            new Layout("Stats").Size(40)
        );

        // Fitness Chart
        var chartContent = CreateSimpleFitnessChart(fitnessHistory, generation);
        layout["Content"]["Chart"].Update(
            new Panel(chartContent)
                .Header("📈 FITNESS EVOLUTION")
                .BorderColor(Color.Green)
        );

        // Statistics Panel  
        var statsContent = $"📊 CURRENT STATISTICS\n\n" +
                          $"Best Individual:\n" +
                          $"  Fitness: {bestFitness:F6}\n\n" +
                          $"Population Stats:\n" +
                          $"  Mean:   {meanFitness:F6}\n" +
                          $"  Std:    {stdFitness:F6}\n\n" +
                          $"Algorithm Parameters:\n" +
                          $"  Population: {parameters.PopulationSize}\n" +
                          $"  CR: {parameters.CrossoverRate}\n" +
                          $"  F: {parameters.DifferentialWeight}";

        layout["Content"]["Stats"].Update(
            new Panel(statsContent)
                .BorderColor(Color.Blue)
        );

        // Footer
        var progress = parameters.MaxGenerations > 0 ? (double)generation / parameters.MaxGenerations : 0;
        var progressBar = CreateProgressBar(progress, 30);
        var footerContent = $"Progress: {progressBar} {progress:P1} | Status: {status}";
        
        layout["Footer"].Update(
            new Panel(footerContent)
                .BorderColor(Color.White)
        );

        return layout;
    }
    
    private static string CreateSimpleFitnessChart(List<double> history, int currentGeneration)
    {
        if (history.Count == 0) return "No data yet - optimization will start soon...";
        
        var result = new List<string>();
        result.Add("Fitness improvements (last " + Math.Min(20, history.Count) + " shown):");
        result.Add("");
        
        var min = history.Min();
        var max = history.Max();
        var range = max - min;
        if (range == 0) range = 1;
        
        // Show improvements chronologically
        for (int i = 0; i < history.Count; i++)
        {
            var normalized = (history[i] - min) / range;
            var barLength = (int)(normalized * 40);
            var bar = new string('█', Math.Max(1, barLength));
            var improvementNumber = i + 1;
            result.Add($"#{improvementNumber,2}: {bar} ({history[i]:F6})");
        }
        
        return string.Join("\n", result);
    }
    
    private static string CreateProgressBar(double progress, int width)
    {
        var filled = (int)(progress * width);
        return $"{new string('#', filled)}{new string('.', width - filled)}";
    }

    private static void RunOptimizationWithUIProgress(DifferentialEvolution.DifferentialEvolution algorithm, IOptimizationProblem problem, AlgorithmParameters algorithmParams)
    {
        Console.WriteLine("🎬 Starting optimization with UI-style progress display...");
        
        // Set up event handler for UI-style progress updates
        algorithm.OnGenerationFinished += (sender, args) =>
        {
            var generation = algorithm.Generations.Last();
            var bestIndividuals = algorithm.GetBestIndividuals(generation);
            var bestFitness = bestIndividuals.First().FitnessValues.First();
            
            // Calculate population statistics
            var allFitnesses = generation.Population.Select(p => p.FitnessValues.First()).ToArray();
            var meanFitness = allFitnesses.Average();
            var stdFitness = allFitnesses.Length > 1 ? 
                Math.Sqrt(allFitnesses.Sum(f => Math.Pow(f - meanFitness, 2)) / allFitnesses.Length) : 0;
            
            // Display UI-style progress
            Console.WriteLine($"📈 Generation {algorithm.Generations.Count,4}: Best={bestFitness:F6}, Mean={meanFitness:F6}, Std={stdFitness:F6}");
            
            // Show progress bar every 10 generations
            if (algorithm.Generations.Count % 10 == 0)
            {
                var progress = (double)algorithm.Generations.Count / algorithmParams.MaxGenerations;
                var progressBarWidth = 30;
                var filled = (int)(progress * progressBarWidth);
                var progressBar = new string('█', filled) + new string('░', progressBarWidth - filled);
                Console.WriteLine($"📊 Progress: [{progressBar}] {progress:P1}");
            }
        };

        Console.WriteLine("▶️  Optimization running...");
        algorithm.Optimize();
        
        Console.WriteLine("\n🎉 Optimization completed!");
        DisplayResults(algorithm, algorithmParams);
    }

    private static void RunOptimizationConsole(DifferentialEvolution.DifferentialEvolution algorithm, IOptimizationProblem problem, AlgorithmParameters algorithmParams)
    {
        AnsiConsole.MarkupLine($"[green]Running {problem.GetType().Name}[/]");
        AnsiConsole.MarkupLine($"[dim]Population size: {algorithmParams.PopulationSize}, Max generations: {algorithmParams.MaxGenerations}[/]");
        AnsiConsole.MarkupLine($"[dim]CR: {algorithmParams.CrossoverRate}, F: {algorithmParams.DifferentialWeight}[/]");

        if (algorithmParams.Verbose)
        {
            algorithm.OnGenerationFinished += (sender, args) =>
            {
                var generation = algorithm.Generations.Last();
                var bestIndividuals = algorithm.GetBestIndividuals(generation);
                var bestFitness = bestIndividuals.First().FitnessValues.First();
                
                AnsiConsole.MarkupLine($"[dim]Generation {algorithm.Generations.Count}: Best fitness = {bestFitness:F6}[/]");
            };
        }
        else
        {
            // Show progress every 10th generation
            algorithm.OnGenerationFinished += (sender, args) =>
            {
                if (algorithm.Generations.Count % 10 == 0)
                {
                    var generation = algorithm.Generations.Last();
                    var bestIndividuals = algorithm.GetBestIndividuals(generation);
                    var bestFitness = bestIndividuals.First().FitnessValues.First();
                    
                    AnsiConsole.MarkupLine($"[dim]Generation {algorithm.Generations.Count}: Best fitness = {bestFitness:F6}[/]");
                }
            };
        }

        algorithm.Optimize();
        DisplayResults(algorithm, algorithmParams);
    }

    private static void DisplayResults(DifferentialEvolution.DifferentialEvolution algorithm, AlgorithmParameters algorithmParams)
    {
        var finalGeneration = algorithm.Generations.Last();
        var bestIndividuals = algorithm.GetBestIndividuals(finalGeneration);
        
        AnsiConsole.MarkupLine($"\n[green]Optimization completed![/]");
        AnsiConsole.MarkupLine($"[dim]Total generations: {algorithm.Generations.Count}[/]");
        AnsiConsole.MarkupLine($"[dim]Best individuals found: {bestIndividuals.Count}[/]");

        var table = new Table();
        table.AddColumn("Individual");
        table.AddColumn("Genes");
        table.AddColumn("Fitness Values");

        for (int i = 0; i < Math.Min(5, bestIndividuals.Count); i++)
        {
            var individual = bestIndividuals[i];
            var genes = string.Join(", ", individual.Genes.Select(g => g.ToString("F6")));
            var fitness = string.Join(", ", individual.FitnessValues.Select(f => f.ToString("F6")));
            
            table.AddRow($"#{i + 1}", genes, fitness);
        }

        if (bestIndividuals.Count > 5)
        {
            table.AddRow("...", "...", "...");
        }

        AnsiConsole.Write(table);

        if (!string.IsNullOrEmpty(algorithmParams.OutputCsv))
        {
            SaveToCsv(bestIndividuals, algorithmParams.OutputCsv);
            AnsiConsole.MarkupLine($"[green]Results saved to {algorithmParams.OutputCsv}[/]");
        }
    }

    private static void SaveToCsv(System.Collections.Immutable.IImmutableList<ParetoEvaluatedIndividual> individuals, string filePath)
    {
        var lines = new System.Collections.Generic.List<string>();
        
        if (individuals.Count > 0)
        {
            var firstIndividual = individuals[0];
            var geneHeaders = Enumerable.Range(0, firstIndividual.Genes.Count).Select(i => $"Gene_{i}");
            var fitnessHeaders = Enumerable.Range(0, firstIndividual.FitnessValues.Count).Select(i => $"Fitness_{i}");
            var headers = string.Join(",", geneHeaders.Concat(fitnessHeaders));
            lines.Add(headers);
        }

        foreach (var individual in individuals)
        {
            var geneValues = individual.Genes.Select(g => g.ToString("F6"));
            var fitnessValues = individual.FitnessValues.Select(f => f.ToString("F6"));
            var row = string.Join(",", geneValues.Concat(fitnessValues));
            lines.Add(row);
        }

        File.WriteAllLines(filePath, lines);
    }
}