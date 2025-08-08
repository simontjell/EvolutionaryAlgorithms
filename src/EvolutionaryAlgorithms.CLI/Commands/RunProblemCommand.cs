using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithm.TerminationCriteria;
using DifferentialEvolution;
using EvolutionaryAlgorithms.CLI.Helpers;

namespace EvolutionaryAlgorithms.CLI.Commands;

public sealed class RunProblemCommand : Command<RunProblemCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Problem type name to run")]
        [CommandOption("-p|--problem-type")]
        public required string ProblemType { get; init; }

        [Description("Population size")]
        [CommandOption("-n|--population-size")]
        [DefaultValue(100)]
        public int PopulationSize { get; init; } = 100;

        [Description("Crossover rate (CR) for differential evolution")]
        [CommandOption("--cr")]
        [DefaultValue(0.5)]
        public double CrossoverRate { get; init; } = 0.5;

        [Description("Differential weight/scaling factor (F)")]
        [CommandOption("--factor")]
        [DefaultValue(1.0)]
        public double DifferentialWeight { get; init; } = 1.0;

        [Description("Maximum number of generations")]
        [CommandOption("-g|--max-generations")]
        [DefaultValue(100)]
        public int MaxGenerations { get; init; } = 100;

        [Description("Random seed for reproducibility")]
        [CommandOption("-s|--seed")]
        public int? Seed { get; init; }

        [Description("Problem dimensions (for problems that support it)")]
        [CommandOption("-d|--dimensions")]
        [DefaultValue(2)]
        public int Dimensions { get; init; } = 2;

        [Description("Show detailed progress during optimization")]
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; init; }

        [Description("Save results to CSV file")]
        [CommandOption("--output-csv")]
        public string? OutputCsv { get; init; }

        [Description("Additional assemblies to search for optimization problems")]
        [CommandOption("-a|--assemblies")]
        public string[]? Assemblies { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            var random = CreateRandom(settings.Seed);
            var problemType = FindProblemType(settings.ProblemType, settings.Assemblies);
            var problem = CreateProblemInstance(problemType, settings.Dimensions, random);
            
            RunOptimization(problem, settings, random);
            
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

    private static System.Type FindProblemType(string problemTypeName, string[]? additionalAssemblyPaths)
    {
        var assemblies = AssemblyHelper.GetAssembliesToSearch(additionalAssemblyPaths);
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name.Equals(problemTypeName, StringComparison.OrdinalIgnoreCase) &&
                                         t.IsClass && 
                                         !t.IsAbstract && 
                                         typeof(IOptimizationProblem).IsAssignableFrom(t));
                
                if (type != null)
                {
                    return type;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Continue searching in other assemblies
            }
        }
        
        throw new ArgumentException($"Problem type '{problemTypeName}' not found. Use 'list-problems' command to see available problems.");
    }

    private static IOptimizationProblem CreateProblemInstance(System.Type problemType, int dimensions, Random random)
    {
        try
        {
            // Try constructor with dimensions and random parameters
            var constructorWithDimensionsAndRandom = problemType.GetConstructor(new[] { typeof(int), typeof(Random) });
            if (constructorWithDimensionsAndRandom != null)
            {
                return (IOptimizationProblem)constructorWithDimensionsAndRandom.Invoke(new object[] { dimensions, random });
            }

            // Try constructor with only random parameter
            var constructorWithRandom = problemType.GetConstructor(new[] { typeof(Random) });
            if (constructorWithRandom != null)
            {
                return (IOptimizationProblem)constructorWithRandom.Invoke(new object[] { random });
            }

            // Try parameterless constructor
            var parameterlessConstructor = problemType.GetConstructor(System.Type.EmptyTypes);
            if (parameterlessConstructor != null)
            {
                return (IOptimizationProblem)parameterlessConstructor.Invoke(null);
            }

            throw new ArgumentException($"Could not find suitable constructor for problem type '{problemType.Name}'");
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Failed to create instance of problem type '{problemType.Name}': {ex.Message}");
        }
    }

    private static void RunOptimization(IOptimizationProblem problem, Settings settings, Random random)
    {
        var parameters = new DifferentialEvolutionOptimizationParameters(
            settings.PopulationSize,
            settings.CrossoverRate,
            settings.DifferentialWeight,
            new GenerationCountTerminationCriterion(settings.MaxGenerations)
        );

        var algorithm = new DifferentialEvolution.DifferentialEvolution(problem, parameters, random);

        AnsiConsole.MarkupLine($"[green]Running {problem.GetType().Name}[/]");
        AnsiConsole.MarkupLine($"[dim]Population size: {settings.PopulationSize}, Max generations: {settings.MaxGenerations}[/]");
        AnsiConsole.MarkupLine($"[dim]CR: {settings.CrossoverRate}, F: {settings.DifferentialWeight}[/]");

        if (settings.Verbose)
        {
            algorithm.OnGenerationFinished += (sender, args) =>
            {
                var generation = algorithm.Generations.Last();
                var bestIndividuals = algorithm.GetBestIndividuals(generation);
                var bestFitness = bestIndividuals.First().FitnessValues.First();
                
                AnsiConsole.MarkupLine($"[dim]Generation {algorithm.Generations.Count}: Best fitness = {bestFitness:F6}[/]");
            };
        }

        algorithm.Optimize();

        DisplayResults(algorithm, settings);
    }

    private static void DisplayResults(DifferentialEvolution.DifferentialEvolution algorithm, Settings settings)
    {
        var finalGeneration = algorithm.Generations.Last();
        var bestIndividuals = algorithm.GetBestIndividuals(finalGeneration);
        
        AnsiConsole.MarkupLine($"\n[green]Optimization completed![/]");
        AnsiConsole.MarkupLine($"[dim]Total generations: {algorithm.Generations.Count}[/]");
        AnsiConsole.MarkupLine($"[dim]Best individuals found: {bestIndividuals.Count}[/]");

        // Display best individual(s)
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

        // Save to CSV if requested
        if (!string.IsNullOrEmpty(settings.OutputCsv))
        {
            SaveToCsv(bestIndividuals, settings.OutputCsv);
            AnsiConsole.MarkupLine($"[green]Results saved to {settings.OutputCsv}[/]");
        }
    }

    private static void SaveToCsv(System.Collections.Immutable.IImmutableList<EvolutionaryAlgorithm.ParetoEvaluatedIndividual> individuals, string filePath)
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