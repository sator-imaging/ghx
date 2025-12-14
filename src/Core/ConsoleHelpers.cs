// Licensed under the MIT License
// https://github.com/sator-imaging/GitHubWorkflow

using System;
using System.CommandLine;

namespace GitHubWorkflow.Core;

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

            WriteError(ex.Message);
            return 1;
        }
    }

    public static void WriteSuccess(string message)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        try
        {
            Console.WriteLine();
            Console.WriteLine($"{message}");
        }
        finally
        {
            Console.ForegroundColor = original;
        }
    }

    private static void WriteError(string message)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        try
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error: {message}");
        }
        finally
        {
            Console.ForegroundColor = original;
        }
    }
}
