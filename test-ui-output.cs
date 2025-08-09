using System;
using System.IO;
using Spectre.Console;

class Program
{
    static void Main()
    {
        // Redirect console output to file
        using var fileStream = new FileStream("ui-output.txt", FileMode.Create, FileAccess.Write);
        using var streamWriter = new StreamWriter(fileStream);
        
        // Test what UI actually outputs
        var panel = new Panel("Status: STOPPED\nGeneration: 0\nBest Fitness: Infinity\n\nPress [Space] to play/pause, [Q] to quit");
        
        // Capture AnsiConsole output
        var originalOut = Console.Out;
        Console.SetOut(streamWriter);
        
        try
        {
            AnsiConsole.Write(panel);
            streamWriter.WriteLine("\n=== END OF PANEL ===");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        
        Console.WriteLine("UI output saved to ui-output.txt");
    }
}