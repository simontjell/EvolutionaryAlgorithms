using System;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;

namespace EvolutionaryAlgorithms.CLI.Cli;

/// <summary>
/// Algorithm parameters that are common across optimization problems
/// </summary>
public record AlgorithmParameters
{
    public int PopulationSize { get; init; } = 100;
    public int MaxGenerations { get; init; } = 100;
    public double CrossoverRate { get; init; } = 0.5;
    public double DifferentialWeight { get; init; } = 1.0;
    public int? Seed { get; init; }
    public bool Verbose { get; init; }
    public string? OutputCsv { get; init; }
}

/// <summary>
/// Interface for optimization problems that want to provide their own CLI command with custom parameters
/// </summary>
public interface IOptimizationProblemCommand<TSettings> where TSettings : CommandSettings
{
    /// <summary>
    /// The command name to register (e.g., "sphere", "booth")
    /// </summary>
    static abstract string CommandName { get; }
    
    /// <summary>
    /// Optional command description
    /// </summary>
    static abstract string? Description { get; }
    
    /// <summary>
    /// Optional command aliases
    /// </summary>
    static abstract string[]? Aliases { get; }
    
    /// <summary>
    /// Create an instance of the optimization problem from the parsed command settings
    /// </summary>
    /// <param name="settings">The parsed command line settings</param>
    /// <param name="random">Random number generator (with seed if specified)</param>
    /// <returns>Configured optimization problem instance</returns>
    static abstract IOptimizationProblem CreateOptimizationProblem(TSettings settings, Random random);
    
    /// <summary>
    /// Get algorithm-specific parameters for the optimization
    /// </summary>
    /// <param name="settings">The parsed command line settings</param>
    /// <returns>Algorithm parameters (population size, generations, etc.)</returns>
    static abstract AlgorithmParameters GetAlgorithmParameters(TSettings settings);
}