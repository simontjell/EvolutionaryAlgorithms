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
    private bool _isPaused = true; // Start paused in UI mode
    private bool _shouldExit;
    private bool _isOptimizationComplete;
    private bool _isStopped = true; // Start stopped
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
        
        _statusMessage = "Ready - Press space to start";
        _currentGeneration = 0;
        
        // Start input handling task
        var inputTask = Task.Run(HandleInputAsync, _cancellationTokenSource.Token);
        
        try
        {
            // Simple working version - just show a basic panel first
            var simplePanel = new Panel("Evolutionary Algorithm UI - Press [Space] to start optimization, [Q] to quit");
            AnsiConsole.Clear();
            AnsiConsole.Write(simplePanel);
            
            // Update loop
            while (!_shouldExit && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(_refreshRateMs, _cancellationTokenSource.Token);
                
                AnsiConsole.Clear();
                AnsiConsole.Write(new Panel($"Status: {(_isStopped ? "STOPPED" : _isPaused ? "PAUSED" : "RUNNING")}\nGeneration: {_currentGeneration}\nBest Fitness: {_bestFitness:F6}\n\nPress [Space] to play/pause, [Q] to quit, [H] for help"));
            }
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
        Console.WriteLine("DEBUG: CreateLayout() - Creating root layout");
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content"),
                new Layout("Footer").Size(3)
            );

        Console.WriteLine("DEBUG: CreateLayout() - Creating header");
        // Header
        layout["Header"].Update(CreateHeader());
        
        Console.WriteLine("DEBUG: CreateLayout() - Splitting content");
        // Content split into left and right panels
        layout["Content"].SplitColumns(
            new Layout("Left"),
            new Layout("Right").Size(30)
        );
        
        Console.WriteLine("DEBUG: CreateLayout() - Creating fitness chart");
        layout["Content"]["Left"].Update(CreateFitnessChart());
        
        Console.WriteLine("DEBUG: CreateLayout() - Splitting right panel");
        layout["Content"]["Right"].SplitRows(
            new Layout("Stats").Size(8),
            new Layout("Controls").Size(6),
            new Layout("Parameters")
        );
        
        Console.WriteLine("DEBUG: CreateLayout() - Creating panels");
        Console.WriteLine("DEBUG: CreateLayout() - Creating stats panel");
        layout["Content"]["Right"]["Stats"].Update(CreateStatsPanel());
        Console.WriteLine("DEBUG: CreateLayout() - Creating controls panel");
        layout["Content"]["Right"]["Controls"].Update(CreateControlsPanel());
        Console.WriteLine("DEBUG: CreateLayout() - Creating parameters panel");
        layout["Content"]["Right"]["Parameters"].Update(CreateParametersPanel());
        
        Console.WriteLine("DEBUG: CreateLayout() - Creating footer");
        // Footer
        layout["Footer"].Update(CreateFooter());

        Console.WriteLine("DEBUG: CreateLayout() - Layout complete");
        return layout;
    }

    private Panel CreateHeader()
    {
        var content = $"Differential Evolution - Gen: {_currentGeneration}/{_parameters.MaxGenerations}";
        return new Panel(content);
    }

    private Panel CreateFitnessChart()
    {
        Console.WriteLine("DEBUG: CreateFitnessChart() - Start");
        // Remove lock for now to see if that's causing the hang
        Console.WriteLine($"DEBUG: CreateFitnessChart() - Fitness history count: {_fitnessHistory.Count}");
        if (_fitnessHistory.Count == 0)
        {
            Console.WriteLine("DEBUG: CreateFitnessChart() - Creating simple panel");
            // Try the absolute simplest panel possible
            return new Panel("No data yet");
        }

        Console.WriteLine("DEBUG: CreateFitnessChart() - Creating ASCII chart");
        var chart = CreateAsciiChart(_fitnessHistory, 25, 10);
        
        Console.WriteLine("DEBUG: CreateFitnessChart() - Creating panel with chart");
        return new Panel(chart)
            .Header("📈 FITNESS EVOLUTION")
            .BorderColor(Color.Green);
    }

    private Panel CreateStatsPanel()
    {
        var content = $"Best: {_bestFitness:F6}\nMean: {_meanFitness:F4}";
        return new Panel(content);
    }

    private Panel CreateControlsPanel()
    {
        var content = "[Space] Play/Pause [Q]uit [H]elp";
        return new Panel(content);
    }

    private Panel CreateParametersPanel()
    {
        var content = $"Pop: {_parameters.PopulationSize}\nGen: {_currentGeneration}/{_parameters.MaxGenerations}";
        return new Panel(content);
    }

    private Panel CreateFooter()
    {
        var status = _isStopped ? "STOPPED" : _isPaused ? "PAUSED" : "RUNNING";
        var content = $"Status: {status} - {_statusMessage}";
        return new Panel(content);
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
        switch (keyInfo.Key)
        {
            case ConsoleKey.Spacebar:
                TogglePlayPause();
                break;
            case ConsoleKey.Q:
                _shouldExit = true;
                break;
            case ConsoleKey.H:
                ShowHelp();
                break;
            case ConsoleKey.S:
                if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    SaveResults(); // Ctrl+S
                }
                else
                {
                    Stop(); // S for stop
                }
                break;
            case ConsoleKey.R:
                Rewind();
                break;
            case ConsoleKey.OemPlus:
            case ConsoleKey.Add:
                IncreaseSpeed();
                break;
            case ConsoleKey.OemMinus:
            case ConsoleKey.Subtract:
                DecreaseSpeed();
                break;
        }
    }

    private void TogglePlayPause()
    {
        if (_isStopped)
        {
            // Start from stopped state
            _isStopped = false;
            _isPaused = false;
            _statusMessage = "Playing... Press 'space' to pause or 'h' for help";
        }
        else
        {
            // Toggle pause/play
            _isPaused = !_isPaused;
            _statusMessage = _isPaused ? "Paused - Press 'space' to resume" : "Playing... Press 'space' to pause";
        }
    }

    private void Stop()
    {
        _isStopped = true;
        _isPaused = true;
        _statusMessage = "Stopped - Press 'space' to start or 'r' to rewind";
    }

    private void Rewind()
    {
        lock (_lockObject)
        {
            // Clear fitness history to restart visualization
            _fitnessHistory.Clear();
            _currentGeneration = 0;
            _bestFitness = double.MaxValue;
            _meanFitness = 0;
            _stdFitness = 0;
            _diversity = 0;
            _convergenceRate = 0;
            _startTime = DateTime.Now;
            _lastGenerationTime = DateTime.Now;
            _generationsPerSecond = 0;
        }
        
        _isStopped = true;
        _isPaused = true;
        _isOptimizationComplete = false;
        ShouldRestart = true; // Signal that algorithm should restart
        _statusMessage = "Rewound to start - Press 'space' to play";
    }

    private Task ShowHelp()
    {
        var helpContent = """
            🆘 HELP - MEDIA PLAYER CONTROLS
            
            [Space] - Play/Pause optimization (main control)
            [S] - Stop optimization (can resume with space)
            [R] - Rewind to beginning (reset all progress) 
            [Q] - Quit application and show final results
            [H] - Show this help
            [Ctrl+S] - Save current results to CSV
            [+] - Increase refresh rate (faster UI updates)
            [-] - Decrease refresh rate (slower UI updates)
            
            🎬 The optimization starts paused - press Space to begin!
            
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
    public bool IsStopped => _isStopped; 
    public bool ShouldExit => _shouldExit;
    public bool ShouldRestart { get; private set; }

    public void SetOptimizationComplete()
    {
        _isOptimizationComplete = true;
        _statusMessage = "Optimization complete! Press 'q' to quit, 'r' to rewind, or 'h' for help";
    }
    
    public void ClearRestartFlag()
    {
        ShouldRestart = false;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        Console.CursorVisible = true;
    }
}