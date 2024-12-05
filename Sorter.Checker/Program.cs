using LongFileSort.Utilities.Helpers;
using LongFileSort.Utilities.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace LongFileSort.Sorter.Checker;

internal class Program
{
    public static void Main(string[] args)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            Console.WriteLine($"Starting at {DateTime.Now}.");
            SorterCheckerHelper.Process(GetOptions(args));
            Console.WriteLine($"Completed in {timer.Elapsed.TotalSeconds} seconds.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine($"Failed after {timer.Elapsed.TotalSeconds} seconds.");
        }
        Console.ReadKey();
    }

    private static SorterOptions GetOptions(string[] args, string defaultFileName = "appsettings.json")
    {
        var fileNotFoundMessage =
            "You must pass the path to an existing configuration file as the first parameter"
            + $"or create a configuration file named {defaultFileName} in the root directory.";

        var optionsPath = args?.Length > 0 ? args[0] : defaultFileName;
        if (!File.Exists(optionsPath)) throw new FileNotFoundException(fileNotFoundMessage);
        return JsonSerializer.Deserialize<SorterOptions>(File.ReadAllText(optionsPath));
    }
}
