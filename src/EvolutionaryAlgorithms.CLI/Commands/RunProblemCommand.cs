using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using EvolutionaryAlgorithm.TerminationCriteria;
using DifferentialEvolution;
using EvolutionaryAlgorithms.CLI.Helpers;
using EvolutionaryAlgorithms.CLI.Commands;

namespace EvolutionaryAlgorithms.CLI.Commands;

public sealed class RunProblemCommand : Command<RunProblemCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Problem command name to run (use 'list-problems' to see available commands)")]
        [CommandArgument(0, "<PROBLEM>")]
        public required string ProblemCommand { get; init; }

        [Description("Assemblies to search for optimization problems (required)")]
        [CommandOption("-a|--assemblies")]
        public required string[] Assemblies { get; init; }
        
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            // Validate assemblies parameter is provided
            if (settings.Assemblies == null || settings.Assemblies.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]Error: Assemblies parameter (-a|--assemblies) is required[/]");
                AnsiConsole.MarkupLine("[dim]Use 'run-problem --help' to see usage information[/]");
                return 1;
            }

            // Discover commands if not already done
            CommandDiscovery.RegisterOptimizationCommands(null, settings.Assemblies);
            
            // Check if this is a discovered optimization command
            if (CommandDiscovery.DiscoveredCommands.TryGetValue(settings.ProblemCommand, out var commandInfo))
            {
                // Get original args and extract the ones after "run-problem <command-name>"
                var allArgs = CommandArgsStorage.Args;
                var runProblemIndex = Array.IndexOf(allArgs, "run-problem");
                var commandNameIndex = runProblemIndex >= 0 ? runProblemIndex + 1 : -1;
                
                var remainingArgs = Array.Empty<string>();
                if (commandNameIndex >= 0 && commandNameIndex < allArgs.Length - 1)
                {
                    remainingArgs = allArgs.Skip(commandNameIndex + 1).ToArray();
                }
                
                // Check if user wants help for this specific command
                if (remainingArgs.Contains("--help") || remainingArgs.Contains("-h"))
                {
                    return ShowOptimizationCommandHelp(commandInfo, settings.ProblemCommand);
                }
                
                // Check if no parameters provided - show help
                if (remainingArgs.Length == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]No parameters provided for '{settings.ProblemCommand}' problem.[/]");
                    AnsiConsole.MarkupLine($"[dim]Use 'run-problem {settings.ProblemCommand} --help' to see available parameters.[/]");
                    return ShowOptimizationCommandHelp(commandInfo, settings.ProblemCommand);
                }
                
                return ExecuteOptimizationCommand(commandInfo, remainingArgs);
            }

            // Show available discovered commands if no valid command provided
            AnsiConsole.MarkupLine($"[red]Unknown problem command: '{settings.ProblemCommand}'[/]");
            
            if (CommandDiscovery.DiscoveredCommands.Count > 0)
            {
                AnsiConsole.MarkupLine("[yellow]Available optimization problem commands:[/]");
                foreach (var (name, info) in CommandDiscovery.DiscoveredCommands)
                {
                    var description = string.IsNullOrEmpty(info.Description) ? "" : $" - {info.Description}";
                    AnsiConsole.MarkupLine($"  [cyan]{name}[/]{description}");
                }
                AnsiConsole.MarkupLine($"[yellow]Use: run-problem <command-name> [options] to run a specific optimization problem[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No optimization problems found. Make sure the assemblies containing your optimization problems are loaded.[/]");
            }
            
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private static int ExecuteOptimizationCommand(CommandDiscovery.CommandInfo commandInfo, string[] remainingArgs)
    {
        try
        {
            // Find the settings type
            var commandInterface = commandInfo.CommandType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && 
                               i.GetGenericTypeDefinition() == typeof(IOptimizationProblemCommand<>));
            
            if (commandInterface == null)
            {
                AnsiConsole.MarkupLine("[red]Invalid command interface[/]");
                return 1;
            }

            var settingsType = commandInterface.GetGenericArguments()[0];
            var genericCommandType = typeof(OptimizationProblemCommand<,>).MakeGenericType(commandInfo.CommandType, settingsType);

            // Create an instance of the generic command
            var commandInstance = Activator.CreateInstance(genericCommandType);
            if (commandInstance == null)
            {
                AnsiConsole.MarkupLine("[red]Could not create command instance[/]");
                return 1;
            }

            // Parse the remaining arguments into the settings type
            var settings = ParseSettingsFromArgs(settingsType, remainingArgs);
            if (settings == null)
            {
                return 1; // Error already reported by ParseSettingsFromArgs
            }

            // Execute directly - we don't need a CommandContext for our implementation
            // Instead we'll pass the arguments to our argument parser

            // Execute the command directly
            var executeMethod = genericCommandType.GetMethod("Execute");
            if (executeMethod == null)
            {
                AnsiConsole.MarkupLine("[red]Execute method not found[/]");
                return 1;
            }

            // Create a minimal context - we only need it for the signature
            var dummyContext = new CommandContext([], new DummyRemainingArguments(), "sphere", null);
            var result = executeMethod.Invoke(commandInstance, new object[] { dummyContext, settings });
            return result is int exitCode ? exitCode : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error executing command: {ex.Message}[/]");
            AnsiConsole.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static object? ParseSettingsFromArgs(Type settingsType, string[] args)
    {
        try
        {
            // This is a simplified argument parser - we'll use reflection to set properties
            var settings = Activator.CreateInstance(settingsType);
            if (settings == null) return null;

            // Parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                
                var propertyName = args[i][2..]; // Remove --
                
                // Find property by name (case insensitive) - also try kebab-case to PascalCase conversion
                var property = settingsType.GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                
                // If not found, try converting kebab-case to PascalCase
                if (property == null)
                {
                    var pascalCaseName = ConvertKebabToPascalCase(propertyName);
                    property = settingsType.GetProperties()
                        .FirstOrDefault(p => string.Equals(p.Name, pascalCaseName, StringComparison.OrdinalIgnoreCase));
                }
                
                // Also try to find by CommandOption attribute
                if (property == null)
                {
                    property = settingsType.GetProperties()
                        .FirstOrDefault(p => HasMatchingCommandOption(p, "--" + propertyName));
                }
                
                if (property?.CanWrite == true)
                {
                    try
                    {
                        var targetType = property.PropertyType;
                        
                        // Handle boolean flags (no value needed)
                        if (targetType == typeof(bool) || targetType == typeof(bool?))
                        {
                            property.SetValue(settings, true);
                        }
                        else
                        {
                            // Need a value for non-boolean properties
                            if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                            {
                                AnsiConsole.MarkupLine($"[red]Parameter '--{propertyName}' requires a value[/]");
                                return null;
                            }
                            
                            var value = args[i + 1];
                            i++; // Skip the value in next iteration
                            
                            // Handle nullable types
                            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                            }
                            
                            var convertedValue = Convert.ChangeType(value, targetType);
                            property.SetValue(settings, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Could not parse value for parameter '--{propertyName}': {ex.Message}[/]");
                        return null;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Unknown parameter: '--{propertyName}'[/]");
                    AnsiConsole.WriteLine("");
                    
                    AnsiConsole.MarkupLine($"[dim]Use --help to see available parameters[/]");
                    
                    return null;
                }
            }
            
            return settings;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error parsing arguments: {ex.Message}[/]");
            return null;
        }
    }

    private static string ConvertKebabToPascalCase(string kebabCase)
    {
        return string.Join("", kebabCase.Split('-')
            .Select(word => char.ToUpper(word[0]) + word[1..].ToLower()));
    }

    private static int ShowOptimizationCommandHelp(CommandDiscovery.CommandInfo commandInfo, string commandName)
    {
        try
        {
            // Find the settings type
            var commandInterface = commandInfo.CommandType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && 
                               i.GetGenericTypeDefinition() == typeof(IOptimizationProblemCommand<>));
            
            if (commandInterface == null)
            {
                AnsiConsole.MarkupLine("[red]Invalid command interface[/]");
                return 1;
            }

            var settingsType = commandInterface.GetGenericArguments()[0];
            
            AnsiConsole.MarkupLine($"[yellow]DESCRIPTION:[/]");
            AnsiConsole.MarkupLine($"{commandInfo.Description ?? "No description available"}");
            AnsiConsole.MarkupLine($"");
            
            AnsiConsole.MarkupLine($"[yellow]USAGE:[/]");
            AnsiConsole.MarkupLine($"    run-problem {commandName} [OPTIONS]");
            AnsiConsole.MarkupLine($"");
            
            // Show available options from the settings type
            var properties = settingsType.GetProperties()
                .Where(p => p.CanWrite && p.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() != null)
                .ToArray();
            
            if (properties.Length > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]OPTIONS:[/]");
                
                // Add standard help option
                AnsiConsole.MarkupLine($"    -h, --help              Show help information");
                
                foreach (var prop in properties)
                {
                    var description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? "";
                    var kebabName = string.Join("-", prop.Name.SelectMany((c, i) => 
                        char.IsUpper(c) && i > 0 ? new[] { '-', char.ToLower(c) } : new[] { char.ToLower(c) }));
                    
                    var typeName = GetFriendlyTypeName(prop.PropertyType);
                    AnsiConsole.MarkupLine($"        --{kebabName,-20} {description} ({typeName})");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]No configurable options available[/]");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error showing help: {ex.Message}[/]");
            return 1;
        }
    }

    private static bool HasMatchingCommandOption(PropertyInfo property, string optionName)
    {
        var commandOptionAttr = property.GetCustomAttribute<Spectre.Console.Cli.CommandOptionAttribute>();
        if (commandOptionAttr == null) return false;
        
        // Use reflection to get the template string from the constructor argument
        var fields = commandOptionAttr.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            var value = field.GetValue(commandOptionAttr);
            if (value is string template && (template.StartsWith("-") || template.StartsWith("--")))
            {
                // Parse template like "-d|--dimensions" or "--param-a"
                var parts = template.Split('|');
                return parts.Any(part => string.Equals(part, optionName, StringComparison.OrdinalIgnoreCase));
            }
        }
        
        return false;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(int) || type == typeof(int?)) return "integer";
        if (type == typeof(double) || type == typeof(double?)) return "number";
        if (type == typeof(string)) return "text";
        if (type == typeof(bool) || type == typeof(bool?)) return "true/false";
        return type.Name.ToLower();
    }

}