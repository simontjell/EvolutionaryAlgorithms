using System.ComponentModel;
using Spectre.Console.Cli;

namespace EvolutionaryAlgorithms.CLI.Cli;

/// <summary>
/// Base settings that all optimization problem commands inherit
/// </summary>
public abstract class OptimizationSettingsBase : CommandSettings
{
    [Description("Population size")]
    [CommandOption("-n|--population-size")]
    [DefaultValue(100)]
    public int PopulationSize { get; init; } = 100;

    [Description("Maximum number of generations")]
    [CommandOption("-g|--max-generations")]
    [DefaultValue(100)]
    public int MaxGenerations { get; init; } = 100;

    [Description("Crossover rate (CR) for differential evolution")]
    [CommandOption("--cr")]
    [DefaultValue(0.5)]
    public double CrossoverRate { get; init; } = 0.5;

    [Description("Differential weight/scaling factor (F)")]
    [CommandOption("--factor")]
    [DefaultValue(1.0)]
    public double DifferentialWeight { get; init; } = 1.0;

    [Description("Random seed for reproducibility")]
    [CommandOption("-s|--seed")]
    public int? Seed { get; init; }

    [Description("Show detailed progress during optimization")]
    [CommandOption("-v|--verbose")]
    public bool Verbose { get; init; }

    [Description("Save results to CSV file")]
    [CommandOption("--output-csv")]
    public string? OutputCsv { get; init; }

    [Description("Show interactive UI during optimization")]
    [CommandOption("--ui|--use-ui")]
    public bool UseUI { get; init; }
}