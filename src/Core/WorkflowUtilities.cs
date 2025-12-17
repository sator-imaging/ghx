// Licensed under the MIT License
// https://github.com/sator-imaging/GitHubWorkflow

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VYaml.Serialization;

namespace GitHubWorkflow.Core;

internal static class WorkflowUtilities
{
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    public static string? ResolveWorkflowPath(string inputName)
    {
        var ext = Path.GetExtension(inputName);
        var candidates = new List<string>();

        if (ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(".github", "workflows", inputName));
        }
        else
        {
            candidates.Add(Path.Combine(".github", "workflows", $"{inputName}.yml"));
            candidates.Add(Path.Combine(".github", "workflows", $"{inputName}.yaml"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public static WorkflowRoot LoadRoot(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return YamlSerializer.Deserialize<WorkflowRoot>(bytes)
            ?? throw new InvalidOperationException("Workflow file is empty or malformed.");
    }

    public static Dictionary<string, InputDefinition> ParseInputs(WorkflowRoot root)
    {
        var inputs = new Dictionary<string, InputDefinition>(StringComparer.OrdinalIgnoreCase);

        if (root.On?.WorkflowCall?.Inputs is not { } inputsNode)
        {
            return inputs;
        }

        foreach (var (name, inputNode) in inputsNode)
        {
            var hasDefault = inputNode.Default is not null;
            var defaultValue = hasDefault ? QuoteIfNeeded(inputNode.Default ?? string.Empty) : null;

            inputs[name] = new InputDefinition(defaultValue, hasDefault);
        }

        return inputs;
    }

    public static Dictionary<string, JobDefinition> GetJobs(WorkflowRoot root)
        => root.Jobs is { } jobs
            ? new Dictionary<string, JobDefinition>(jobs, StringComparer.OrdinalIgnoreCase)
            : [];

    public static List<Dictionary<string, string>> BuildMatrix(JobDefinition job)
    {
        if (job.Strategy?.Matrix is not { } matrix)
        {
            return [[]];
        }

        var axes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, seq) in matrix)
        {
            axes[key] = [.. seq.Select(v => QuoteIfNeeded(v))];
        }

        return GenerateCombinations(axes);
    }

    public static IEnumerable<string> ExtractRunSteps(string jobName, JobDefinition job)
    {
        if (job.Steps is not { } steps)
        {
            return [];
        }

        var runs = new List<string>();
        var stepIndex = 0;
        foreach (var step in steps)
        {
            stepIndex++;
            if (step.Shell is not null)
            {
                var stepName = step.Name ?? $"#{stepIndex}";
                throw new InvalidOperationException($"Job '{jobName}' step '{stepName}' specifies shell; custom shells are not supported.");
            }

            if (step.Run is { } run)
            {
                runs.Add(run);
            }
        }

        return runs;
    }

    public static string BuildCommandScript(Dictionary<string, InputDefinition> inputs, Dictionary<string, JobDefinition> jobs, bool useCmdFormatting, bool onceOnly)
    {
        var commands = new List<string>();

        if (useCmdFormatting)
        {
            commands.Add("@ECHO OFF");
            commands.Add(string.Empty);
        }
        else
        {
            if (!OperatingSystem.IsWindows())  // TODO: not work as expected on WSL. why??
            {
                commands.Add("set -e");
                commands.Add(string.Empty);
            }
        }

        if (inputs.Count > 0)
        {
            commands.Add("echo inputs:");
            foreach (var input in inputs.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var value = input.Value.DefaultValue ?? string.Empty;
                commands.Add($"echo   {input.Key}={value}");
            }

        }

        foreach (var job in jobs)
        {
            var matrixCombos = BuildMatrix(job.Value);
            var originalMatrixCount = matrixCombos.Count;
            if (onceOnly && matrixCombos.Count > 1)
            {
                matrixCombos = [matrixCombos[0]];
            }
            var runSteps = ExtractRunSteps(job.Key, job.Value).ToList();

            if (runSteps.Count == 0)
            {
                continue;
            }

            commands.Add(string.Empty);
            commands.Add(string.Empty);
            commands.Add($"echo {new string('=', 76)}");
            commands.Add($"echo job '{job.Key}': matrix count={originalMatrixCount}");
            commands.Add($"echo {new string('=', 76)}");

            foreach (var combo in matrixCombos)
            {
                var (supportedRunner, runnerName) = ValidateRunsOn(job.Key, job.Value, combo, inputs);

                commands.Add(string.Empty);
                commands.Add(string.Empty);
                commands.Add($"echo {new string('-', 76)}");
                commands.Add($"echo {job.Key}: {DescribeMatrix(combo)}");
                commands.Add($"echo {new string('-', 76)}");
                commands.Add(string.Empty);

                if (!supportedRunner)
                {
                    commands.Add($"echo Unsupported runner: {runnerName}");
                    continue;
                }

                foreach (var run in runSteps)
                {
                    var command = ConvertRunStep(run, inputs, combo, useCmdFormatting);
                    commands.Add(command.Trim());
                }
            }
        }

        if (useCmdFormatting)
        {
            commands.Add(string.Empty);
            commands.Add(string.Empty);
            commands.Add("GOTO :EOF");
            commands.Add(string.Empty);
            commands.Add(":ERROR");
            commands.Add("  ECHO.");
            commands.Add("  ECHO ======= ERROR OCCURRED =======");
            commands.Add("  ECHO.");
            commands.Add("  EXIT 310");
        }

        return string.Join(useCmdFormatting ? Environment.NewLine : "\n", commands);  // "\n" for WSL compatibility
    }

    public static string ConvertRunStep(string run, IReadOnlyDictionary<string, InputDefinition> inputs, IReadOnlyDictionary<string, string> matrix, bool useCmdFormatting)
    {
        var placeholderResolver = CreatePlaceholderResolver(inputs, matrix);

        var lines = run.Split(LineSeparators, StringSplitOptions.None);
        var processed = new List<string>(capacity: lines.Length);

        bool lineContinues = false;
        foreach (var line in lines)
        {
            var withoutComment = RegexHelpers.InlineCommentPattern.Replace(line, string.Empty).TrimEnd();

            var trimmedStart = withoutComment.TrimStart();
            if (string.IsNullOrWhiteSpace(trimmedStart) ||
                trimmedStart.StartsWith('#'))
            {
                continue;
            }

            var replacedLine = RegexHelpers.PlaceholderPattern.Replace(withoutComment, new MatchEvaluator(placeholderResolver));
            replacedLine = RegexHelpers.StepSummaryRedirectPattern.Replace(replacedLine, string.Empty).TrimEnd();

            if (useCmdFormatting)
            {
                replacedLine = RegexHelpers.DollarPositionalPattern.Replace(replacedLine, "%$1");
                replacedLine = ReplaceTrailingBackslash(replacedLine);
            }

            replacedLine = RegexHelpers.SleepCommandPattern.Replace(replacedLine, "ghx sleep $1");

            if (string.IsNullOrWhiteSpace(replacedLine))
            {
                processed.Add(string.Empty);  // Add empty line to indicate it is completely removed
                continue;
            }

            if (useCmdFormatting)
            {
                if (!lineContinues)
                {
                    replacedLine = $"CALL {replacedLine}";
                }
                else
                {
                    replacedLine = $"     {replacedLine}";
                }

                lineContinues = replacedLine.EndsWith('^');

                if (!lineContinues)
                {
                    replacedLine += "  || CALL :ERROR";
                }
            }

            if (replacedLine.Contains('$'))
            {
                var variableCheckTarget = RegexHelpers.DollarPositionalPattern.Replace(replacedLine, string.Empty);
                if (variableCheckTarget.Contains('$'))
                {
                    throw new InvalidOperationException($"Unsupported template expression found: {variableCheckTarget}");
                }
            }

            processed.Add(replacedLine);
        }

        return string.Join(Environment.NewLine, processed);
    }

    private static List<Dictionary<string, string>> GenerateCombinations(Dictionary<string, List<string>> matrix)
    {
        var result = new List<Dictionary<string, string>>();

        void Recurse(IReadOnlyList<string> keys, int index, Dictionary<string, string> current)
        {
            if (index == keys.Count)
            {
                result.Add(new Dictionary<string, string>(current, StringComparer.OrdinalIgnoreCase));
                return;
            }

            var key = keys[index];
            foreach (var value in matrix[key])
            {
                current[key] = value;
                Recurse(keys, index + 1, current);
            }
        }

        Recurse([.. matrix.Keys], 0, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        return result;
    }

    private static string DescribeMatrix(Dictionary<string, string> combo)
    {
        if (combo.Count == 0)
        {
            return "no matrix";
        }

        return string.Join(" ", combo.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private static (bool IsSupported, string RunnerName) ValidateRunsOn(string jobName, JobDefinition job, IReadOnlyDictionary<string, string> matrix, IReadOnlyDictionary<string, InputDefinition> inputs)
    {
        var runner = ResolveRunsOn(jobName, job, matrix, inputs);
        return (runner.Equals("ubuntu-latest", StringComparison.OrdinalIgnoreCase), runner);
    }

    private static string ResolveRunsOn(string jobName, JobDefinition job, IReadOnlyDictionary<string, string> matrix, IReadOnlyDictionary<string, InputDefinition> inputs)
    {
        if (job.RunsOn is not { } runsOn)
        {
            throw new InvalidOperationException($"Job '{jobName}' must specify runs-on.");
        }

        var placeholderResolver = CreatePlaceholderResolver(inputs, matrix);

        string ReplaceRunnerValue(string value)
        {
            return RegexHelpers.PlaceholderPattern.Replace(value ?? string.Empty, new MatchEvaluator(placeholderResolver)).Trim();
        }

        return runsOn switch
        {
            string scalar => ReplaceRunnerValue(scalar),
            IEnumerable<object> sequence => string.Join(" ", sequence
                .Select(obj =>
                {
                    if (obj is not string scalar)
                    {
                        throw new InvalidOperationException($"Job '{jobName}' runs-on entries must be scalars.");
                    }
                    return ReplaceRunnerValue(scalar);
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            _ => throw new InvalidOperationException($"Job '{jobName}' has invalid runs-on definition.")
        };
    }

    private static string ReplaceTrailingBackslash(string line) =>
        line.EndsWith('\\')
            ? line[..^1] + '^'
            : line;

    private static Func<Match, string> CreatePlaceholderResolver(IReadOnlyDictionary<string, InputDefinition> inputs, IReadOnlyDictionary<string, string> matrix) =>
        match =>
        {
            var scope = match.Groups[1].Value;
            var key = match.Groups[2].Value;

            if (scope.Equals("inputs", StringComparison.OrdinalIgnoreCase))
            {
                if (!inputs.TryGetValue(key, out var input))
                {
                    throw new InvalidOperationException($"Input '{key}' not defined.");
                }

                if (!input.HasDefault)
                {
                    throw new InvalidOperationException($"Input '{key}' has no default value.");
                }

                return input.DefaultValue ?? string.Empty;
            }

            if (!matrix.TryGetValue(key, out var matrixValue))
            {
                throw new InvalidOperationException($"Matrix value '{key}' not defined.");
            }

            return matrixValue;
        };

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOf(' ') < 0)
        {
            return value;
        }

        return $"\"{value}\"";
    }
}
