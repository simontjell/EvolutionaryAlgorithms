using System;
using System.Threading.Tasks;
using Spectre.Console;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Testing basic Spectre.Console...");
        
        // Test basic panel
        var panel = new Panel("Hello World!")
            .Header("Test Panel")
            .BorderColor(Color.Blue);
            
        AnsiConsole.Write(panel);
        Console.WriteLine("Press any key to test Live display...");
        Console.ReadKey();
        
        // Test Live display
        await AnsiConsole.Live(new Panel("Loading..."))
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                for (int i = 0; i < 5; i++)
                {
                    ctx.UpdateTarget(new Panel($"Count: {i}")
                        .Header("Live Test")
                        .BorderColor(Color.Green));
                    await Task.Delay(1000);
                }
            });
            
        Console.WriteLine("Done!");
    }
}