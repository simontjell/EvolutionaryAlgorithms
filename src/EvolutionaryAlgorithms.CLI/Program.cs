using Spectre.Console.Cli;
using EvolutionaryAlgorithms.CLI.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddCommand<ListProblemsCommand>("list-problems")
        .WithAlias("list")
        .WithDescription("List available optimization problems");
});

return app.Run(args);