using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Spectre.Console;

namespace EvolutionaryAlgorithms.CLI.Helpers;

public static class AssemblyHelper
{
    public static System.Collections.Generic.List<Assembly> GetAssembliesToSearch(string[]? additionalAssemblyPaths)
    {
        var assemblies = new System.Collections.Generic.List<Assembly>();
        
        // Add currently loaded assemblies
        assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies());
        
        // Add additional assemblies if specified
        if (additionalAssemblyPaths != null)
        {
            foreach (var pathPattern in additionalAssemblyPaths)
            {
                try
                {
                    var matchingPaths = ExpandWildcardPath(pathPattern);
                    
                    foreach (var path in matchingPaths)
                    {
                        try
                        {
                            var assembly = Assembly.LoadFrom(path);
                            assemblies.Add(assembly);
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning: Could not load assembly '{path}': {ex.Message}[/]");
                        }
                    }
                    
                    if (matchingPaths.Length == 0)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning: No files match pattern '{pathPattern}'[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error processing assembly pattern '{pathPattern}': {ex.Message}[/]");
                }
            }
        }
        
        return assemblies;
    }

    private static string[] ExpandWildcardPath(string pathPattern)
    {
        // If no wildcards, return as-is (but check if file exists)
        if (!pathPattern.Contains('*') && !pathPattern.Contains('?'))
        {
            return File.Exists(pathPattern) ? new[] { pathPattern } : Array.Empty<string>();
        }

        try
        {
            // Handle directory wildcards using recursive search
            var directoryPart = Path.GetDirectoryName(pathPattern) ?? "";
            var filePattern = Path.GetFileName(pathPattern);

            // If directory part contains wildcards, we need recursive search
            if (directoryPart.Contains('*') || directoryPart.Contains('?'))
            {
                var searchOption = SearchOption.AllDirectories;
                var rootDir = GetRootDirectoryFromPattern(directoryPart);
                
                if (Directory.Exists(rootDir))
                {
                    return Directory.GetFiles(rootDir, filePattern, searchOption)
                        .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                                   f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        .Where(f => MatchesDirectoryPattern(Path.GetDirectoryName(f) ?? "", directoryPart))
                        .ToArray();
                }
                
                return Array.Empty<string>();
            }

            // Simple case: only filename has wildcards
            var directory = string.IsNullOrEmpty(directoryPart) ? Directory.GetCurrentDirectory() : directoryPart;
            
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directory, filePattern)
                .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                           f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string GetRootDirectoryFromPattern(string directoryPattern)
    {
        var parts = directoryPattern.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootParts = parts.TakeWhile(part => !part.Contains('*') && !part.Contains('?')).ToArray();
        
        if (rootParts.Length == 0)
        {
            return Directory.GetCurrentDirectory();
        }
        
        return Path.Combine(rootParts);
    }

    private static bool MatchesDirectoryPattern(string actualPath, string pattern)
    {
        // Simple implementation - could be more sophisticated
        var actualParts = actualPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var patternParts = pattern.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        if (patternParts.Length > actualParts.Length)
            return false;
            
        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var actualPart = actualParts[actualParts.Length - patternParts.Length + i];
            
            if (!MatchesWildcard(actualPart, patternPart))
                return false;
        }
        
        return true;
    }

    private static bool MatchesWildcard(string text, string pattern)
    {
        // Simple wildcard matching - * matches any sequence, ? matches single char
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}