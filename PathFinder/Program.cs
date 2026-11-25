using PathFinder.Entities;

namespace PathFinder;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? filePath;
        if(args.Length > 0)
        {
            filePath = args[0];
        }
        else
        {
            Console.WriteLine("Please enter the path to the input file:");
            filePath = Console.ReadLine();
        }
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.WriteLine("Invalid file path.");
            WaitForExit();
            return 2;
        }

        try
        {
            using var document = await DirectionDocument.LoadAsync(filePath, CancellationToken.None);

            var items = document.Items
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (items.Count == 0)
            {
                Console.WriteLine("No items found");
                WaitForExit();
                return 1;
            }

            Console.WriteLine("Available items:\n");

            for (var i = 0; i < items.Count; i++)
                Console.WriteLine($"[{i + 1}] - {items[i].Name}");

            await SelectLoopAsync(document, items);

            return 0;
        }        
        catch (Exception e)
        {
            Report(e);
            WaitForExit();
            return 2;
        }
    }

    private static void Report(Exception ex)
    {
        var message = ex switch
        {
            FileNotFoundException => "File not found.",
            DirectoryNotFoundException => "Directory not found.",
            InvalidDataException => ex.Message,
            UnauthorizedAccessException => "Access to file denied.",
            _ => $"Something went wrong: {ex.Message}",
        };


        Console.WriteLine("Error: ");
        Console.WriteLine(message);
    }

    private static async Task SelectLoopAsync(DirectionDocument document, List<Item> items)
    {
        while (true)
        {
            Console.Write("\nWhat item would you like to search for? Or press Enter for exit.\n");

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) // End on empty
                return;

            if (!int.TryParse(input, out var num) || num < 1 || num > items.Count)
            {
                Console.WriteLine("Not a valid choice");
                continue;
            }

            var path = await document.GetDirectionAsync(items[num - 1], CancellationToken.None);
            foreach (var direction in path)
            {
                Console.WriteLine(direction);
            }
        }
    }

    private static void WaitForExit()
    {
        // Cant read key if input is rederected
        if (Console.IsInputRedirected)
            return;

        Console.WriteLine();
        Console.Write("Press any key to exit...");
        Console.ReadKey();
    }
}