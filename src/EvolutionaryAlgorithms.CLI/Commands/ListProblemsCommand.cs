using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using EvolutionaryAlgorithm;
using EvolutionaryAlgorithms.CLI.Cli;
using EvolutionaryAlgorithms.CLI.Helpers;

namespace EvolutionaryAlgorithms.CLI.Commands;

public sealed class ListProblemsCommand : Command<ListProblemsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Assemblies to search for optimization problems (required)")]
        [CommandOption("-a|--assemblies")]
        public required string[] Assemblies { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // Validate assemblies parameter is provided
        if (settings.Assemblies == null || settings.Assemblies.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Assemblies parameter (-a|--assemblies) is required[/]");
            AnsiConsole.MarkupLine("[dim]Use 'list-problems --help' to see usage information[/]");
            return 1;
        }

        // Discover commands dynamically
        CommandDiscovery.RegisterOptimizationCommands(null, settings.Assemblies);
        
        DisplayCommands();

        return 0;
    }

    private static void DisplayCommands()
    {
        if (CommandDiscovery.DiscoveredCommands.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No optimization problem commands found.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Command Name");
        table.AddColumn("Description");
        table.AddColumn("Type");

        foreach (var (commandName, commandInfo) in CommandDiscovery.DiscoveredCommands.OrderBy(kvp => kvp.Key))
        {
            table.AddRow(
                commandName,
                commandInfo.Description ?? "[grey]<no description>[/]",
                commandInfo.CommandType.FullName ?? "[grey]<unknown>[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[green]Found {CommandDiscovery.DiscoveredCommands.Count} optimization problem command(s).[/]");
        AnsiConsole.MarkupLine("[dim]Use 'run-problem <command-name>' to run a specific optimization problem.[/]");
    }
}