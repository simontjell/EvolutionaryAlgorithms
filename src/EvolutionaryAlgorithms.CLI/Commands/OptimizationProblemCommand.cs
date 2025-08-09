using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using EvolutionaryAlgorithm.TerminationCriteria;
using DifferentialEvolution;

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

        AnsiConsole.MarkupLine($"[green]Running {problem.GetType().Name}[/]");
        AnsiConsole.MarkupLine($"[dim]Population size: {algorithmParams.PopulationSize}, Max generations: {algorithmParams.MaxGenerations}[/]");
        AnsiConsole.MarkupLine($"[dim]CR: {algorithmParams.CrossoverRate}, F: {algorithmParams.DifferentialWeight}[/]");

        // TODO: Replace with rich live UI
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
            // Use live UI by default - this would need the UI components from CLI project
            // For now just show simple progress
            algorithm.OnGenerationFinished += (sender, args) =>
            {
                if (algorithm.Generations.Count % 10 == 0) // Show every 10th generation
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