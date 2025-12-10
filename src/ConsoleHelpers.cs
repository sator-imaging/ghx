using System;
using System.CommandLine;

namespace GitHubWorkflow;

internal static class ConsoleHelpers
{
    public static int RunWithErrorHandling(RootCommand rootCommand, Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            rootCommand.Parse("-h").Invoke();  // no way to show help!!

            WriteError($"× Error: {ex.Message}");
            return 1;
        }
    }

    private static void WriteError(string message)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        try
        {
            Console.Error.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = original;
        }
    }
}
