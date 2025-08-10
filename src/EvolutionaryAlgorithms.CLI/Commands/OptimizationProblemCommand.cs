using System;
using System.Collections.Generic;
using System.Globalization;
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
            
            // Minimal delay only when UI is active (reduce from 50ms to 10ms)
            if (!uiManager.ShouldExit)
            {
                Task.Delay(10).Wait();
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
        var paretoFront = new List<ParetoEvaluatedIndividual>();
        var allIndividuals = new List<ParetoEvaluatedIndividual>();
        var status = "PAUSED";
        var isPaused = true;
        var shouldStep = false;
        var shouldExit = false;
        var isInitialized = false;
        
        // Create initial layout
        var layout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, paretoFront, status, allIndividuals);
        
        // Set up Ctrl-C handler to restore cursor
        ConsoleCancelEventHandler cancelHandler = (sender, e) =>
        {
            Console.CursorVisible = true;
            shouldExit = true;
            e.Cancel = true; // Don't exit immediately, let us clean up
        };
        Console.CancelKeyPress += cancelHandler;
        
        // Use Live display for interactive control
        try
        {
            AnsiConsole.Live(layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .Start(ctx =>
                {
                    // Update initial display
                    ctx.UpdateTarget(layout);
                    
                    // Main interactive loop
                    while (!shouldExit && currentGeneration < algorithmParams.MaxGenerations)
                    {
                        // Handle keyboard input
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            switch (key.Key)
                            {
                                case ConsoleKey.Spacebar:
                                    isPaused = !isPaused;
                                    status = isPaused ? "PAUSED" : "RUNNING";
                                    break;
                                case ConsoleKey.RightArrow:
                                case ConsoleKey.Enter:
                                    if (isPaused)
                                    {
                                        shouldStep = true;
                                    }
                                    break;
                                case ConsoleKey.Q:
                                case ConsoleKey.Escape:
                                    shouldExit = true;
                                    break;
                            }
                            
                            // Update layout with new status
                            var statusLayout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, paretoFront, status, allIndividuals);
                            ctx.UpdateTarget(statusLayout);
                        }
                        
                        // Initialize optimization if not done yet
                        if (!isInitialized && (!isPaused || shouldStep))
                        {
                            isInitialized = true;
                            shouldStep = false;
                            
                            // Set up event handler to control step-by-step execution  
                            var wasRunning = false;
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
                                
                                // Check if this is a multi-objective problem
                                var isMultiObjective = bestIndividuals.First().FitnessValues.Count > 1;
                                
                                if (isMultiObjective)
                                {
                                    // For multi-objective: store Pareto front and all individuals for scatter plot
                                    paretoFront.Clear();
                                    allIndividuals.Clear();
                                    paretoFront.AddRange(bestIndividuals.Take(50)); // Limit to 50 Pareto points
                                    allIndividuals.AddRange(generation.Population.Take(100)); // Limit to 100 total points
                                    bestFitness = newBestFitness; // Still track for header display
                                }
                                else
                                {
                                    // For single-objective: only add to fitness history if fitness improved
                                    // For minimization problems, improvement means lower fitness value
                                    var isFirstGeneration = fitnessHistory.Count == 0;
                                    var hasImproved = isFirstGeneration || newBestFitness < bestFitness;
                                    
                                    if (hasImproved)
                                    {
                                        bestFitness = newBestFitness;
                                        fitnessHistory.Add(bestFitness);
                                        if (fitnessHistory.Count > 20) fitnessHistory.RemoveAt(0); // Keep last 20
                                    }
                                }
                                
                                // Update the layout
                                var updatedLayout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, paretoFront, status, allIndividuals);
                                ctx.UpdateTarget(updatedLayout);
                                
                                // Pause after each generation if we were stepping or if paused
                                if (isPaused || shouldStep)
                                {
                                    status = "PAUSED";
                                    shouldStep = false;
                                    
                                    // Wait until unpaused or step requested
                                    while ((isPaused && !shouldStep) && !shouldExit && currentGeneration < algorithmParams.MaxGenerations)
                                    {
                                        Thread.Sleep(50);
                                    }
                                    
                                    if (shouldStep)
                                    {
                                        shouldStep = false;
                                        status = "RUNNING";
                                    }
                                    else if (!isPaused)
                                    {
                                        status = "RUNNING";
                                        Thread.Sleep(20); // Reduced delay for better performance (200ms -> 20ms)
                                    }
                                }
                                else if (!isPaused)
                                {
                                    Thread.Sleep(20); // Reduced delay for better performance (200ms -> 20ms)
                                }
                            };
                            
                            // Start optimization in background thread
                            var optimizationThread = new Thread(() =>
                            {
                                try
                                {
                                    algorithm.Optimize();
                                }
                                catch (Exception ex)
                                {
                                    // Handle any exceptions
                                    status = "ERROR: " + ex.Message;
                                }
                            });
                            optimizationThread.Start();
                        }
                        else
                        {
                            // Small delay to prevent high CPU usage when paused
                            Thread.Sleep(50);
                        }
                    }
                    
                    // Final update
                    status = shouldExit ? "STOPPED" : "COMPLETED";
                    var finalLayout = CreatePanelLayout(problemName, algorithmParams, currentGeneration, bestFitness, meanFitness, stdFitness, fitnessHistory, paretoFront, status, allIndividuals);
                    ctx.UpdateTarget(finalLayout);
                    
                    // Hold the display briefly
                    Thread.Sleep(1000);
                });
        }
        finally
        {
            // Always ensure cursor is restored, even if an exception occurs
            Console.CursorVisible = true;
            // Remove the cancel handler
            Console.CancelKeyPress -= cancelHandler;
        }
        
        DisplayResults(algorithm, algorithmParams);
    }
    
    private static Layout CreatePanelLayout(string problemName, AlgorithmParameters parameters, int generation, double bestFitness, double meanFitness, double stdFitness, List<double> fitnessHistory, List<ParetoEvaluatedIndividual> paretoFront, string status, List<ParetoEvaluatedIndividual> allIndividuals = null)
    {
        // Create main layout
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(4),
                new Layout("Content"),
                new Layout("Footer").Size(4)  // Increased size for controls
            );

        // Header Panel - show all fitness values for multi-objective problems
        var bestFitnessDisplay = "";
        if (paretoFront.Count > 0)
        {
            var isMultiObjective = paretoFront.First().FitnessValues.Count > 1;
            if (isMultiObjective)
            {
                // Show all fitness values for multi-objective - use InvariantCulture to avoid comma issues
                var fitnessValues = paretoFront.First().FitnessValues.Select(f => f.ToString("F6", CultureInfo.InvariantCulture));
                var allFitnessValues = string.Join(" | ", fitnessValues);
                bestFitnessDisplay = $"Best Fitness: [{allFitnessValues}]";
            }
            else
            {
                bestFitnessDisplay = $"Best Fitness: {bestFitness.ToString("F6", CultureInfo.InvariantCulture)}";
            }
        }
        else
        {
            bestFitnessDisplay = $"Best Fitness: {bestFitness.ToString("F6", CultureInfo.InvariantCulture)}";
        }
        
        var headerContent = $"🧬 Differential Evolution - {problemName}\n" +
                           $"Generation: {generation}/{parameters.MaxGenerations}  |  {bestFitnessDisplay.EscapeMarkup()}\n" +
                           $"Population: {parameters.PopulationSize}  |  Status: {status}";
        
        layout["Header"].Update(
            new Panel(headerContent)
                .Header("OPTIMIZATION DASHBOARD")
                .BorderColor(status == "COMPLETED" ? Color.Green : status == "RUNNING" ? Color.Blue : Color.Yellow)
        );

        // Chart - either fitness evolution or Pareto scatter plot
        string chartContent;
        string chartHeader;
        
        if (paretoFront.Count > 0 && paretoFront.First().FitnessValues.Count > 1)
        {
            // Multi-objective: show Pareto front scatter plot with all individuals
            chartContent = CreateParetoScatterPlot(paretoFront, allIndividuals);
            chartHeader = "🎯 PARETO FRONT (X-Y SCATTER)";
        }
        else
        {
            // Single-objective: show fitness evolution
            chartContent = CreateSimpleFitnessChart(fitnessHistory, generation);
            chartHeader = "📈 FITNESS EVOLUTION";
        }
        
        // Content - split to create space at top and chart at bottom
        layout["Content"].SplitRows(
            new Layout("Spacer").Size(2),
            new Layout("Chart")
        );
        
        layout["Content"]["Chart"].Update(
            new Panel(chartContent)
                .Header(chartHeader)
                .BorderColor(Color.Green)
        );
        
        // Empty spacer at top
        layout["Content"]["Spacer"].Update(new Panel("").NoBorder());

        // Footer
        var progress = parameters.MaxGenerations > 0 ? (double)generation / parameters.MaxGenerations : 0;
        var progressBar = CreateProgressBar(progress, 30);
        var controlsText = status == "PAUSED" ? 
            "SPACE = Play | → = Step | Q = Quit" : 
            "SPACE = Pause | Q = Quit";
        var footerContent = $"Progress: {progressBar} {progress.ToString("P1", CultureInfo.InvariantCulture)}\n{controlsText}";
        
        layout["Footer"].Update(
            new Panel(footerContent)
                .BorderColor(Color.White)
        );

        return layout;
    }
    
    private static string CreateParetoScatterPlot(List<ParetoEvaluatedIndividual> paretoFront, List<ParetoEvaluatedIndividual> allIndividuals = null)
    {
        if (paretoFront.Count == 0) return "No Pareto solutions yet - optimization will start soon...";
        
        // Use all individuals if provided, otherwise just Pareto front
        var individualsToPlot = allIndividuals ?? paretoFront;
        
        var result = new List<string>();
        result.Add($"Population scatter plot ({individualsToPlot.Count} points):");
        result.Add("Green = Pareto-optimal (ParetoRank=0), Yellow = Non-optimal");
        result.Add("");
        
        // Extract X and Y coordinates from all individuals to determine bounds
        var allXValues = individualsToPlot.Select(p => p.FitnessValues[0]).ToList();
        var allYValues = individualsToPlot.Select(p => p.FitnessValues[1]).ToList();
        
        var xMin = allXValues.Min();
        var xMax = allXValues.Max();
        var yMin = allYValues.Min();
        var yMax = allYValues.Max();
        
        var xRange = xMax - xMin;
        var yRange = yMax - yMin;
        
        if (xRange == 0) xRange = 1;
        if (yRange == 0) yRange = 1;
        
        // Create ASCII scatter plot (40x20 grid)
        const int width = 40;
        const int height = 20;
        var grid = new (char symbol, bool isPareto)[height][];
        for (int i = 0; i < height; i++)
        {
            grid[i] = new (char, bool)[width];
            Array.Fill(grid[i], (' ', false));
        }
        
        // Plot all individuals
        foreach (var individual in individualsToPlot)
        {
            var x = individual.FitnessValues[0];
            var y = individual.FitnessValues[1];
            
            var xPos = (int)((x - xMin) / xRange * (width - 1));
            var yPos = height - 1 - (int)((y - yMin) / yRange * (height - 1)); // Flip Y axis
            
            xPos = Math.Max(0, Math.Min(width - 1, xPos));
            yPos = Math.Max(0, Math.Min(height - 1, yPos));
            
            // Use ParetoRank directly - 0 means Pareto-optimal
            bool isPareto = individual.ParetoRank == 0;
            grid[yPos][xPos] = ('●', isPareto);
        }
        
        // Convert grid to colored strings
        for (int y = 0; y < height; y++)
        {
            var line = "";
            for (int x = 0; x < width; x++)
            {
                var (symbol, isPareto) = grid[y][x];
                if (symbol == '●')
                {
                    // Add color markup for Spectre.Console
                    if (isPareto)
                        line += "[green]●[/]"; // Green for Pareto-optimal
                    else
                        line += "[yellow]●[/]"; // Yellow for non-optimal
                }
                else
                {
                    line += symbol;
                }
            }
            result.Add(line);
        }
        
        // Add axis labels
        result.Add(new string('─', width) + "+");
        result.Add($"X: ({xMin.ToString("F3", CultureInfo.InvariantCulture)}, {xMax.ToString("F3", CultureInfo.InvariantCulture)})");
        result.Add($"Y: ({yMin.ToString("F3", CultureInfo.InvariantCulture)}, {yMax.ToString("F3", CultureInfo.InvariantCulture)})");
        
        return string.Join("\n", result);
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
            result.Add($"#{improvementNumber,2}: {bar} ({history[i].ToString("F6", CultureInfo.InvariantCulture)})");
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
            Console.WriteLine($"📈 Generation {algorithm.Generations.Count,4}: Best={bestFitness.ToString("F6", CultureInfo.InvariantCulture)}, Mean={meanFitness.ToString("F6", CultureInfo.InvariantCulture)}, Std={stdFitness.ToString("F6", CultureInfo.InvariantCulture)}");
            
            // Show progress bar every 10 generations
            if (algorithm.Generations.Count % 10 == 0)
            {
                var progress = (double)algorithm.Generations.Count / algorithmParams.MaxGenerations;
                var progressBarWidth = 30;
                var filled = (int)(progress * progressBarWidth);
                var progressBar = new string('█', filled) + new string('░', progressBarWidth - filled);
                Console.WriteLine($"📊 Progress: [{progressBar}] {progress.ToString("P1", CultureInfo.InvariantCulture)}");
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
                
                AnsiConsole.MarkupLine($"[dim]Generation {algorithm.Generations.Count}: Best fitness = {bestFitness.ToString("F6", CultureInfo.InvariantCulture)}[/]");
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
                    
                    AnsiConsole.MarkupLine($"[dim]Generation {algorithm.Generations.Count}: Best fitness = {bestFitness.ToString("F6", CultureInfo.InvariantCulture)}[/]");
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
            var genes = string.Join(", ", individual.Genes.Select(g => g.ToString("F6", CultureInfo.InvariantCulture)));
            var fitness = string.Join(", ", individual.FitnessValues.Select(f => f.ToString("F6", CultureInfo.InvariantCulture)));
            
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
            var geneValues = individual.Genes.Select(g => g.ToString("F6", CultureInfo.InvariantCulture));
            var fitnessValues = individual.FitnessValues.Select(f => f.ToString("F6", CultureInfo.InvariantCulture));
            var row = string.Join(",", geneValues.Concat(fitnessValues));
            lines.Add(row);
        }

        File.WriteAllLines(filePath, lines);
    }
}