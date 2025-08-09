using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;

namespace EvolutionaryAlgorithms.CLI.UI;

/// <summary>
/// Manages the interactive terminal UI for optimization visualization
/// </summary>
public class UIManager : IDisposable
{
    private readonly string _problemName;
    private readonly AlgorithmParameters _parameters;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<double> _fitnessHistory;
    private readonly object _lockObject = new();
    
    // Current state
    private int _currentGeneration;
    private double _bestFitness = double.MaxValue;
    private double _meanFitness;
    private double _stdFitness;
    private double _diversity;
    private double _convergenceRate;
    private double _generationsPerSecond;
    private DateTime _startTime;
    private DateTime _lastGenerationTime;
    private bool _isPaused;
    private bool _shouldExit;
    private string _statusMessage = "Initializing...";

    // UI Settings
    private int _refreshRateMs = 100; // 10 Hz default
    private const int MaxHistorySize = 50;

    public UIManager(string problemName, AlgorithmParameters parameters)
    {
        _problemName = problemName;
        _parameters = parameters;
        _cancellationTokenSource = new CancellationTokenSource();
        _fitnessHistory = new List<double>();
        _startTime = DateTime.Now;
        _lastGenerationTime = DateTime.Now;
    }

    /// <summary>
    /// Start the interactive UI in a separate task
    /// </summary>
    public async Task StartAsync()
    {
        Console.CursorVisible = false;
        AnsiConsole.Clear();
        
        // Start input handling task
        var inputTask = Task.Run(HandleInputAsync, _cancellationTokenSource.Token);
        
        _statusMessage = "Running... Press 'h' for help";
        
        try
        {
            await AnsiConsole.Live(CreateLayout())
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    while (!_shouldExit && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        if (!_isPaused)
                        {
                            ctx.UpdateTarget(CreateLayout());
                        }
                        
                        await Task.Delay(_refreshRateMs, _cancellationTokenSource.Token);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    /// <summary>
    /// Update the UI with new generation data
    /// </summary>
    public void UpdateGeneration(int generation, double bestFitness, double meanFitness, double stdFitness, IEnumerable<ParetoEvaluatedIndividual>? individuals = null)
    {
        lock (_lockObject)
        {
            _currentGeneration = generation;
            _bestFitness = bestFitness;
            _meanFitness = meanFitness;
            _stdFitness = stdFitness;
            
            // Update fitness history
            _fitnessHistory.Add(bestFitness);
            if (_fitnessHistory.Count > MaxHistorySize)
            {
                _fitnessHistory.RemoveAt(0);
            }
            
            // Calculate performance metrics
            var now = DateTime.Now;
            var timeSinceLastGeneration = (now - _lastGenerationTime).TotalSeconds;
            if (timeSinceLastGeneration > 0)
            {
                _generationsPerSecond = 1.0 / timeSinceLastGeneration;
            }
            _lastGenerationTime = now;
            
            // Calculate diversity if individuals provided
            if (individuals != null)
            {
                _diversity = CalculateDiversity(individuals);
            }
            
            // Calculate convergence rate
            _convergenceRate = CalculateConvergenceRate();
        }
    }

    /// <summary>
    /// Create the main UI layout
    /// </summary>
    private Layout CreateLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content"),
                new Layout("Footer").Size(3)
            );

        // Header
        layout["Header"].Update(CreateHeader());
        
        // Content split into left and right panels
        layout["Content"].SplitColumns(
            new Layout("Left"),
            new Layout("Right").Size(30)
        );
        
        layout["Content"]["Left"].Update(CreateFitnessChart());
        layout["Content"]["Right"].SplitRows(
            new Layout("Stats").Size(8),
            new Layout("Controls").Size(6),
            new Layout("Parameters")
        );
        
        layout["Content"]["Right"]["Stats"].Update(CreateStatsPanel());
        layout["Content"]["Right"]["Controls"].Update(CreateControlsPanel());
        layout["Content"]["Right"]["Parameters"].Update(CreateParametersPanel());
        
        // Footer
        layout["Footer"].Update(CreateFooter());

        return layout;
    }

    private Panel CreateHeader()
    {
        var title = $"🧬 Differential Evolution - {_problemName}";
        var metrics = $"⏱️  Generation: {_currentGeneration}/{_parameters.MaxGenerations}        🎯 Best Fitness: {_bestFitness:F6}";
        var stats = $"👥 Population: {_parameters.PopulationSize}            🔥 Convergence: {_convergenceRate:F1}%";
        var performance = $"⚡ Speed: {_generationsPerSecond:F1} gen/sec        📊 Diversity: {_diversity:F3}";
        
        var content = $"{metrics}\n{stats}\n{performance}";
        
        return new Panel(content)
            .Header(title)
            .BorderColor(_isPaused ? Color.Yellow : Color.Blue);
    }

    private Panel CreateFitnessChart()
    {
        lock (_lockObject)
        {
            if (_fitnessHistory.Count == 0)
            {
                return new Panel("No data yet...")
                    .Header("📈 FITNESS EVOLUTION")
                    .BorderColor(Color.Green);
            }

            var chart = CreateAsciiChart(_fitnessHistory, 25, 10);
            
            return new Panel(chart)
                .Header("📈 FITNESS EVOLUTION")
                .BorderColor(Color.Green);
        }
    }

    private Panel CreateStatsPanel()
    {
        var content = $"""
            🧪 CURRENT GENERATION
            
              Best Individual
              Fitness: {_bestFitness:F6}
            
              Population Stats
              Mean:   {_meanFitness:F4}
              Std:    {_stdFitness:F4}
            """;
        
        return new Panel(content)
            .BorderColor(Color.Blue);
    }

    private Panel CreateControlsPanel()
    {
        var content = $"""
            🎛️  CONTROLS
            
            [P]ause/Resume  [S]ave Results
            [Q]uit          [+/-] Speed
            [R]eset Stats   [Tab] Next View
            [H]elp
            """;
        
        return new Panel(content)
            .BorderColor(Color.Yellow);
    }

    private Panel CreateParametersPanel()
    {
        var content = $"""
            📋 ALGORITHM PARAMETERS
            
            Population:     {_parameters.PopulationSize}
            Crossover (CR): {_parameters.CrossoverRate}
            Factor (F):     {_parameters.DifferentialWeight}
            Seed:           {_parameters.Seed?.ToString() ?? "Random"}
            """;
        
        return new Panel(content)
            .BorderColor(Color.Red);
    }

    private Panel CreateFooter()
    {
        var status = _isPaused ? "⏸️  PAUSED" : "▶️  RUNNING";
        var content = $"💬 Status: {status} - {_statusMessage}";
        
        return new Panel(content)
            .BorderColor(_isPaused ? Color.Yellow : Color.Green);
    }

    private string CreateAsciiChart(List<double> data, int width, int height)
    {
        if (data.Count == 0) return "No data";
        
        var min = data.Min();
        var max = data.Max();
        var range = max - min;
        
        if (range == 0) range = 1; // Avoid division by zero
        
        var lines = new string[height];
        for (int i = 0; i < height; i++)
        {
            lines[i] = new string(' ', width);
        }
        
        // Plot points
        for (int i = 0; i < data.Count && i < width; i++)
        {
            var normalizedValue = (data[i] - min) / range;
            var yPos = height - 1 - (int)(normalizedValue * (height - 1));
            yPos = Math.Max(0, Math.Min(height - 1, yPos));
            
            var line = lines[yPos].ToCharArray();
            line[i] = '●';
            lines[yPos] = new string(line);
        }
        
        // Add Y-axis labels
        var result = new List<string>();
        for (int i = 0; i < height; i++)
        {
            var value = max - (i * range / (height - 1));
            var label = value.ToString("F1").PadLeft(4);
            result.Add($"{label} ┤{lines[i]}");
        }
        
        // Add X-axis
        var xAxis = "     ┼" + new string('─', width);
        result.Add(xAxis);
        result.Add($"      0{new string(' ', width - 8)}{data.Count - 1} Gen");
        
        return string.Join("\n", result);
    }

    private async Task HandleInputAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    await HandleKeyPress(keyInfo);
                }
                
                await Task.Delay(50, _cancellationTokenSource.Token); // Check for input every 50ms
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private async Task HandleKeyPress(ConsoleKeyInfo keyInfo)
    {
        switch (char.ToLower(keyInfo.KeyChar))
        {
            case 'p':
                TogglePause();
                break;
            case 'q':
                _shouldExit = true;
                break;
            case 'h':
                ShowHelp();
                break;
            case 's':
                SaveResults();
                break;
            case 'r':
                ResetStats();
                break;
            case '+':
            case '=':
                IncreaseSpeed();
                break;
            case '-':
                DecreaseSpeed();
                break;
        }
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        _statusMessage = _isPaused ? "Paused - Press 'p' to resume" : "Running... Press 'h' for help";
    }

    private Task ShowHelp()
    {
        var helpContent = """
            🆘 HELP - INTERACTIVE CONTROLS
            
            [P] - Pause/Resume optimization
            [Q] - Quit application  
            [H] - Show this help
            [S] - Save current results to CSV
            [R] - Reset performance statistics
            [+] - Increase refresh rate (faster UI)
            [-] - Decrease refresh rate (slower UI)
            
            Press any key to continue...
            """;
        
        var helpPanel = new Panel(helpContent)
            .Header("🆘 Help")
            .BorderColor(Color.Red);
            
        AnsiConsole.Clear();
        AnsiConsole.Write(helpPanel);
        
        Console.ReadKey(true);
        AnsiConsole.Clear();
        
        return Task.CompletedTask;
    }

    private void SaveResults()
    {
        // TODO: Implement save functionality
        _statusMessage = "Save functionality not yet implemented";
    }

    private void ResetStats()
    {
        lock (_lockObject)
        {
            _startTime = DateTime.Now;
            _lastGenerationTime = DateTime.Now;
            _generationsPerSecond = 0;
        }
        _statusMessage = "Statistics reset";
    }

    private void IncreaseSpeed()
    {
        _refreshRateMs = Math.Max(50, _refreshRateMs - 25); // Min 20 Hz
        _statusMessage = $"Refresh rate: {1000.0 / _refreshRateMs:F1} Hz";
    }

    private void DecreaseSpeed()
    {
        _refreshRateMs = Math.Min(1000, _refreshRateMs + 25); // Max 1 Hz
        _statusMessage = $"Refresh rate: {1000.0 / _refreshRateMs:F1} Hz";
    }

    private double CalculateDiversity(IEnumerable<ParetoEvaluatedIndividual> individuals)
    {
        // Simple diversity metric based on fitness variance
        var fitnesses = individuals.Select(i => i.FitnessValues.First()).ToArray();
        if (fitnesses.Length <= 1) return 0;
        
        var mean = fitnesses.Average();
        var variance = fitnesses.Sum(f => Math.Pow(f - mean, 2)) / fitnesses.Length;
        return Math.Sqrt(variance);
    }

    private double CalculateConvergenceRate()
    {
        if (_fitnessHistory.Count < 10) return 0;
        
        // Simple convergence metric: how much fitness improved in last 10 generations
        var recent = _fitnessHistory.TakeLast(10).ToArray();
        var improvement = recent.First() - recent.Last();
        var maxPossibleImprovement = recent.First();
        
        if (maxPossibleImprovement == 0) return 100;
        
        return Math.Min(100, Math.Max(0, (improvement / Math.Abs(maxPossibleImprovement)) * 100));
    }

    public bool IsPaused => _isPaused;
    public bool ShouldExit => _shouldExit;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        Console.CursorVisible = true;
    }
}