using System;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithms.CLI.Cli;
using EvolutionaryAlgorithms.CLI.Commands;

namespace EvolutionaryAlgorithms.CLI.Helpers;

/// <summary>
/// Discovers and registers optimization problem commands via reflection
/// </summary>
public static class CommandDiscovery
{
    /// <summary>
    /// Discover all optimization problem commands and store them for use by list-problems and run-problem
    /// </summary>
    public static void RegisterOptimizationCommands(ICommandApp? app, string[]? additionalAssemblyPaths = null)
    {
        var assemblies = AssemblyHelper.GetAssembliesToSearch(additionalAssemblyPaths);
        var commandTypes = FindOptimizationCommands(assemblies);
        
        // Store discovered command types for RunProblemCommand to use
        DiscoveredCommands.Clear();
        
        foreach (var commandType in commandTypes)
        {
            try
            {
                var commandName = GetStaticProperty(commandType, "CommandName") as string;
                var description = GetStaticProperty(commandType, "Description") as string;
                
                if (!string.IsNullOrEmpty(commandName))
                {
                    // Store for RunProblemCommand and ListProblemsCommand
                    DiscoveredCommands[commandName] = new CommandInfo(commandType, description);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not process command type '{commandType.Name}': {ex.Message}[/]");
            }
        }
    }

    public static System.Collections.Generic.Dictionary<string, CommandInfo> DiscoveredCommands { get; } = new();

    public record CommandInfo(System.Type CommandType, string? Description);

    private static System.Collections.Generic.List<System.Type> FindOptimizationCommands(System.Collections.Generic.List<Assembly> assemblies)
    {
        var commands = new System.Collections.Generic.List<System.Type>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && 
                               !t.IsAbstract && 
                               IsOptimizationCommand(t))
                    .ToList();
                
                commands.AddRange(types);
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loadableTypes = ex.Types
                    .Where(t => t != null)
                    .Where(t => t.IsClass && 
                               !t.IsAbstract && 
                               IsOptimizationCommand(t))
                    .ToList();

                commands.AddRange(loadableTypes);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not load types from assembly '{assembly.FullName}': {ex.Message}[/]");
            }
        }

        return commands.Distinct().ToList();
    }

    private static bool IsOptimizationCommand(System.Type type)
    {
        // Check if type implements IOptimizationProblemCommand<T>
        return type.GetInterfaces()
            .Any(i => i.IsGenericType && 
                     i.GetGenericTypeDefinition() == typeof(IOptimizationProblemCommand<>));
    }


    private static object? GetStaticProperty(System.Type type, string propertyName)
    {
        return type.GetProperty(propertyName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null);
    }
}