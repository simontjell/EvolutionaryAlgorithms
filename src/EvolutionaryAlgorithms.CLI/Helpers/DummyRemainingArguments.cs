using System.Collections.Generic;
using System.Linq;
using Spectre.Console.Cli;

namespace EvolutionaryAlgorithms.CLI.Helpers;

/// <summary>
/// Dummy implementation of IRemainingArguments for creating CommandContext instances
/// </summary>
public class DummyRemainingArguments : IRemainingArguments
{
    public IReadOnlyList<string> Raw { get; } = [];
    public ILookup<string, string?> Parsed { get; } = Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
}