using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithms.CLI.Cli;
using EvolutionaryAlgorithms.CLI.Commands;
using EvolutionaryAlgorithms.CLI.Helpers;

var app = new CommandApp();

app.Configure(config =>
{
    // Add traditional commands
    config.AddCommand<ListProblemsCommand>("list-problems")
        .WithAlias("list")
        .WithDescription("List available optimization problems");
    
    config.AddCommand<RunProblemCommand>("run-problem")
        .WithAlias("run")
        .WithDescription("Run an optimization problem using differential evolution");
});

// Store args globally so RunProblemCommand can access them
CommandArgsStorage.Args = args;

// Force load assemblies and discover commands early
try 
{
    var currentDir = AppContext.BaseDirectory;
    var testAssemblyPath = Path.Combine(currentDir, "OptimizationProblemTests.dll");
    
    if (File.Exists(testAssemblyPath))
    {
        System.Reflection.Assembly.LoadFrom(testAssemblyPath);
    }
    
    CommandDiscovery.RegisterOptimizationCommands(null, null);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error discovering commands: {ex.Message}[/]");
}

// Handle special --help cases manually before Spectre.Console processes them
if (args.Length >= 3 && args[0] == "run-problem" && args.Contains("--help") || args.Contains("-h"))
{
    var problemNameIndex = Array.IndexOf(args, "run-problem") + 1;
    if (problemNameIndex >= args.Length) return 1;
    
    var problemName = args[problemNameIndex];
    
    // Parse assembly arguments if present
    string[]? additionalAssemblies = null;
    var assemblyFlagIndex = Array.IndexOf(args, "-a");
    if (assemblyFlagIndex == -1) assemblyFlagIndex = Array.IndexOf(args, "--assemblies");
    
    if (assemblyFlagIndex >= 0 && assemblyFlagIndex < args.Length - 1)
    {
        additionalAssemblies = new[] { args[assemblyFlagIndex + 1] };
    }
    
    // Force load assemblies
    try 
    {
        var currentDir = AppContext.BaseDirectory;
        var testAssemblyPath = Path.Combine(currentDir, "OptimizationProblemTests.dll");
        
        if (File.Exists(testAssemblyPath))
        {
            System.Reflection.Assembly.LoadFrom(testAssemblyPath);
        }
        
        CommandDiscovery.RegisterOptimizationCommands(null, additionalAssemblies);
        
        if (CommandDiscovery.DiscoveredCommands.TryGetValue(problemName, out var commandInfo))
        {
            return ShowOptimizationCommandHelp(commandInfo, problemName);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Unknown problem command: '{problemName}'[/]");
            return 1;
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        return 1;
    }
}

return app.Run(args);

static int ShowOptimizationCommandHelp(CommandDiscovery.CommandInfo commandInfo, string commandName)
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
        
        AnsiConsole.MarkupLine("[yellow]DESCRIPTION:[/]");
        AnsiConsole.WriteLine($"{commandInfo.Description ?? "No description available"}");
        AnsiConsole.WriteLine("");
        
        AnsiConsole.MarkupLine("[yellow]USAGE:[/]");
        AnsiConsole.WriteLine($"    run-problem {commandName} [OPTIONS]");
        AnsiConsole.WriteLine("");
        
        AnsiConsole.MarkupLine("[yellow]NOTE:[/]");
        AnsiConsole.WriteLine("Option names shown below may use simplified names. Some commands may use");
        AnsiConsole.WriteLine("different option names (e.g. --param-a instead of --a). Try both variants.");
        AnsiConsole.WriteLine("");
        
        // Show available options from the settings type
        var properties = settingsType.GetProperties()
            .Where(p => p.CanWrite && p.GetCustomAttribute<DescriptionAttribute>() != null)
            .ToArray();
        
        if (properties.Length > 0)
        {
            // Separate problem-specific and base optimization parameters
            var baseProperties = properties
                .Where(p => typeof(OptimizationSettingsBase).GetProperties().Any(bp => bp.Name == p.Name))
                .ToArray();
            var problemSpecificProperties = properties
                .Where(p => !typeof(OptimizationSettingsBase).GetProperties().Any(bp => bp.Name == p.Name))
                .ToArray();

            // Show problem-specific options first
            if (problemSpecificProperties.Length > 0)
            {
                AnsiConsole.MarkupLine("[yellow]PROBLEM-SPECIFIC OPTIONS:[/]");
                AnsiConsole.WriteLine("    -h, --help              Show help information");
                
                foreach (var prop in problemSpecificProperties)
                {
                    ShowPropertyHelp(prop);
                }
                AnsiConsole.WriteLine("");
            }

            // Show base optimization options
            if (baseProperties.Length > 0)
            {
                AnsiConsole.MarkupLine("[yellow]OPTIMIZATION ALGORITHM OPTIONS:[/]");
                
                foreach (var prop in baseProperties)
                {
                    ShowPropertyHelp(prop);
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No configurable options available[/]");
        }
        
        return 0;
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error showing help: {ex.Message}[/]");
        return 1;
    }
}

static void ShowPropertyHelp(PropertyInfo prop)
{
    var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
    
    // Get the actual command option names from [CommandOption] attribute
    var commandOptionAttr = prop.GetCustomAttribute<Spectre.Console.Cli.CommandOptionAttribute>();
    var optionNames = new List<string>();
    
    if (commandOptionAttr != null)
    {
        // Use reflection to get the template string from the constructor argument
        var fields = commandOptionAttr.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Try to find template or option name in fields/properties
        string? template = null;
        foreach (var field in fields)
        {
            var value = field.GetValue(commandOptionAttr);
            if (value is string str && (str.StartsWith("-") || str.StartsWith("--")))
            {
                template = str;
                break;
            }
        }
        
        if (template != null)
        {
            // Parse template like "-d|--dimensions" or "--param-a"
            var parts = template.Split('|');
            foreach (var part in parts)
            {
                if (part.StartsWith("--"))
                {
                    optionNames.Add(part);
                }
            }
        }
    }
    
    // Fallback to kebab-case property name if no [CommandOption] found
    if (optionNames.Count == 0)
    {
        var kebabName = string.Concat(prop.Name.Select((c, i) => 
            char.IsUpper(c) && i > 0 ? "-" + char.ToLower(c) : char.ToLower(c).ToString()));
        optionNames.Add("--" + kebabName);
    }
    
    var optionNamesStr = string.Join(", ", optionNames);
    var typeName = GetFriendlyTypeName(prop.PropertyType);
    
    // Get default value
    var defaultValue = GetDefaultValue(prop);
    var defaultValueStr = defaultValue != null ? $" (default: {defaultValue})" : "";
    
    AnsiConsole.WriteLine($"        {optionNamesStr,-25} {description} ({typeName}){defaultValueStr}");
}

static string GetFriendlyTypeName(Type type)
{
    if (type == typeof(int) || type == typeof(int?)) return "integer";
    if (type == typeof(double) || type == typeof(double?)) return "number";
    if (type == typeof(string)) return "text";
    if (type == typeof(bool) || type == typeof(bool?)) return "true/false";
    return type.Name.ToLower();
}

static object? GetDefaultValue(PropertyInfo prop)
{
    var defaultValueAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
    if (defaultValueAttr != null)
    {
        return defaultValueAttr.Value;
    }
    
    // Try to get default from property initializer by creating instance
    try
    {
        var declaringType = prop.DeclaringType;
        if (declaringType != null)
        {
            var instance = Activator.CreateInstance(declaringType);
            return prop.GetValue(instance);
        }
    }
    catch
    {
        // Ignore errors when trying to get default value
    }
    
    return null;
}