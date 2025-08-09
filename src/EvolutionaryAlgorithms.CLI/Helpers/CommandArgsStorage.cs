namespace EvolutionaryAlgorithms.CLI.Helpers;

/// <summary>
/// Global storage for command line arguments to support argument passing to dynamically discovered commands
/// </summary>
public static class CommandArgsStorage
{
    public static string[] Args { get; set; } = [];
}