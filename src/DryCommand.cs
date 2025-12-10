using System;

namespace GitHubWorkflow;

internal sealed class DryCommand
{
    public static void Run(string path, bool useCmdFormatting, bool onceOnly)
    {
        var root = WorkflowUtilities.LoadRoot(path);

        var inputs = WorkflowUtilities.ParseInputs(root);
        var jobs = WorkflowUtilities.GetJobs(root);
        if (jobs.Count == 0)
        {
            throw new InvalidOperationException("No jobs found in workflow.");
        }

        var script = WorkflowUtilities.BuildCommandScript(inputs, jobs, useCmdFormatting, onceOnly);
        Console.WriteLine(script);
    }
}
