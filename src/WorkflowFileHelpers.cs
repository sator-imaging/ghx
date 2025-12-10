using System;
using System.IO;

namespace GitHubWorkflow;

internal static class WorkflowFileHelpers
{
    public static void EnsureWorkflowExists(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        Console.Write($"Workflow '{fileName}' not found. Create new? [y/N]: ");
        var answer = Console.ReadLine();
        if (!IsYes(answer))
        {
            throw new InvalidOperationException($"Workflow '{fileName}' not found and creation declined.");
        }

        var result = NewCommand.Run(path);
        if (result != 0)
        {
            throw new InvalidOperationException($"Workflow '{fileName}' could not be created.");
        }
    }

    private static bool IsYes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
