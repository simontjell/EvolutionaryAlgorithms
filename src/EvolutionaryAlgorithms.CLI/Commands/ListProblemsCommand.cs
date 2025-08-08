using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;

namespace EvolutionaryAlgorithms.CLI.Commands;

public sealed class ListProblemsCommand : Command<ListProblemsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Additional assemblies to search for optimization problems")]
        [CommandOption("-a|--assemblies")]
        public string[]? Assemblies { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var assemblies = GetAssembliesToSearch(settings.Assemblies);
        var problems = FindOptimizationProblems(assemblies);

        DisplayProblems(problems);

        return 0;
    }

    private static System.Collections.Generic.List<Assembly> GetAssembliesToSearch(string[]? additionalAssemblyPaths)
    {
        var assemblies = new System.Collections.Generic.List<Assembly>();
        
        // Add currently loaded assemblies
        assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies());
        
        // Add additional assemblies if specified
        if (additionalAssemblyPaths != null)
        {
            foreach (var path in additionalAssemblyPaths)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    assemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error loading assembly '{path}': {ex.Message}[/]");
                }
            }
        }
        
        return assemblies;
    }

    private static System.Collections.Generic.List<System.Type> FindOptimizationProblems(System.Collections.Generic.List<Assembly> assemblies)
    {
        var problems = new System.Collections.Generic.List<System.Type>();
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && 
                               !t.IsAbstract && 
                               typeof(IOptimizationProblem).IsAssignableFrom(t))
                    .ToList();
                
                problems.AddRange(types);
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Handle cases where some types can't be loaded
                var loadableTypes = ex.Types
                    .Where(t => t != null)
                    .Where(t => t.IsClass && 
                               !t.IsAbstract && 
                               typeof(IOptimizationProblem).IsAssignableFrom(t))
                    .ToList();
                
                problems.AddRange(loadableTypes);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not load types from assembly '{assembly.FullName}': {ex.Message}[/]");
            }
        }
        
        return problems.Distinct().ToList();
    }

    private static void DisplayProblems(System.Collections.Generic.List<System.Type> problems)
    {
        if (problems.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No optimization problems found.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Namespace");
        table.AddColumn("Assembly");

        foreach (var problem in problems.OrderBy(p => p.FullName))
        {
            table.AddRow(
                problem.Name,
                problem.Namespace ?? "[grey]<no namespace>[/]",
                problem.Assembly.GetName().Name ?? "[grey]<unknown>[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[green]Found {problems.Count} optimization problem(s).[/]");
    }
}