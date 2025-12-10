using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace GitHubWorkflow;

internal static class ArgumentHelpers
{
    public static string ValidateWorkflowName(ArgumentResult result)
    {
        var value = result.Tokens.SingleOrDefault()?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            result.AddError("workflow name is required.");
            return string.Empty;
        }

        if (value.Contains('/') || value.Contains('\\'))
        {
            result.AddError("workflow name must be a file name without any path separators.");
            return string.Empty;
        }

        return value;
    }

    public static string ParseWorkflowPath(ArgumentResult result)
    {
        var value = ValidateWorkflowName(result);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var ext = Path.GetExtension(value);
        if (!string.IsNullOrEmpty(ext) &&
            !ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError("workflow name extension must be .yml or .yaml when specified.");
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(ext))
        {
            return Path.Combine(".github", "workflows", value);
        }

        var resolved = WorkflowUtilities.ResolveWorkflowPath(value);
        if (!string.IsNullOrEmpty(resolved))
        {
            return resolved;
        }

        return Path.Combine(".github", "workflows", $"{value}.yml");
    }

    public static Action<CommandResult> ValidateCmdVsWsl(Option<bool> cmdFlag, Option<bool> wslFlag)
    {
        return cmdResult =>
        {
            var useCmd = cmdResult.GetValue(cmdFlag);
            var useWsl = cmdResult.GetValue(wslFlag);

            if (useCmd && useWsl)
            {
                cmdResult.AddError("Options --cmd and --wsl cannot be used together.");
            }
        };
    }
}
