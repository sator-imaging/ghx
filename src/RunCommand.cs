using System;
using System.Diagnostics;
using System.IO;

namespace GitHubWorkflow;

internal sealed class RunCommand
{
    public static int Run(string path, bool useCmdFormatting, bool useWsl, bool onceOnly)
    {
        if (!OperatingSystem.IsWindows() && useCmdFormatting)
        {
            throw new InvalidOperationException("--cmd is only supported on Windows.");
        }

        var root = WorkflowUtilities.LoadRoot(path);

        var inputs = WorkflowUtilities.ParseInputs(root);
        var jobs = WorkflowUtilities.GetJobs(root);
        if (jobs.Count == 0)
        {
            throw new InvalidOperationException("No jobs found in workflow.");
        }

        var script = WorkflowUtilities.BuildCommandScript(inputs, jobs, useCmdFormatting, onceOnly);

        var extension = useCmdFormatting ? ".bat" : ".sh";
        var tempPath = Path.Combine(Path.GetTempPath(), $"SatorImaging.{nameof(GitHubWorkflow)}-{Guid.NewGuid():N}{extension}");

        try
        {
            File.WriteAllText(tempPath, script);

            var command = BuildRunnerCommand(tempPath, useCmdFormatting, useWsl);
            Console.WriteLine(new string('=', 76));
            Console.WriteLine(BuildDisplay(command));
            Console.WriteLine(new string('=', 76));
            return RunRunnerCommand(command);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to run generated script: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    private static (string FileName, string Arguments) BuildRunnerCommand(string scriptPath, bool useCmdFormatting, bool useWsl)
    {
        if (OperatingSystem.IsWindows())
        {
            if (useCmdFormatting)
            {
                return ("cmd.exe", $"/c \"{scriptPath}\"");
            }

            if (useWsl)
            {
                var wslPath = ToWslPath(scriptPath);
                return ("wsl", $"bash -el \"{wslPath}\"");
            }

            throw new InvalidOperationException("On Windows, specify --cmd or --wsl.");
        }

        return ("/usr/bin/env", $"bash -el \"{scriptPath}\"");
    }

    private static int RunRunnerCommand((string FileName, string Arguments) command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                UseShellExecute = false,
            }
        };

        process.Start();
        process.WaitForExit();

        return process.ExitCode;
    }

    private static string BuildDisplay((string FileName, string Arguments) command)
    {
        return $"{command.FileName} {command.Arguments}";
    }

    private static string ToWslPath(string windowsPath)
    {
        var full = Path.GetFullPath(windowsPath);

        if (full.Length < 2 || full[1] != ':')
        {
            return full.Replace('\\', '/');
        }

        var drive = char.ToLowerInvariant(full[0]);
        var rest = full.Substring(2).Replace('\\', '/');
        if (!rest.StartsWith('/'))
        {
            rest = "/" + rest;
        }

        return $"/mnt/{drive}{rest}";
    }
}
