using Spectre.Console.Cli;
using EvolutionaryAlgorithms.CLI.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddCommand<ListProblemsCommand>("list-problems")
        .WithAlias("list")
        .WithDescription("List available optimization problems");
    
    config.AddCommand<RunProblemCommand>("run-problem")
        .WithAlias("run")
        .WithDescription("Run an optimization problem using differential evolution");
});

return app.Run(args);