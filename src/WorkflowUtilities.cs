using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace GitHubWorkflow;

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

    public static YamlMappingNode LoadRoot(string path)
    {
        var yaml = new YamlStream();
        using (var reader = new StreamReader(path))
        {
            yaml.Load(reader);
        }

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidOperationException("workflow file is empty or malformed.");
        }

        return root;
    }

    public static Dictionary<string, InputDefinition> ParseInputs(YamlMappingNode root)
    {
        var inputs = new Dictionary<string, InputDefinition>(StringComparer.OrdinalIgnoreCase);

        if (!TryGetMapping(root, "on", out var onNode) ||
            !TryGetMapping(onNode, "workflow_call", out var workflowCall) ||
            !TryGetMapping(workflowCall, "inputs", out var inputsNode))
        {
            return inputs;
        }

        foreach (var child in inputsNode.Children)
        {
            var name = child.Key.ToString();
            if (child.Value is not YamlMappingNode inputNode)
            {
                continue;
            }

            var hasDefault = inputNode.Children.TryGetValue(new YamlScalarNode("default"), out var defaultNode);
            var defaultValue = hasDefault ? QuoteIfNeeded(defaultNode?.ToString() ?? string.Empty) : null;

            inputs[name] = new InputDefinition(defaultValue, hasDefault);
        }

        return inputs;
    }

    public static Dictionary<string, YamlMappingNode> GetJobs(YamlMappingNode root)
    {
        var result = new Dictionary<string, YamlMappingNode>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetMapping(root, "jobs", out var jobsNode))
        {
            return result;
        }

        foreach (var job in jobsNode.Children)
        {
            if (job.Value is YamlMappingNode jobMap)
            {
                result[job.Key.ToString()] = jobMap;
            }
        }

        return result;
    }

    public static List<Dictionary<string, string>> BuildMatrix(YamlMappingNode job)
    {
        if (!TryGetMapping(job, "strategy", out var strategy) ||
            !TryGetMapping(strategy, "matrix", out var matrixNode))
        {
            return [[]];
        }

        var axes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var axis in matrixNode.Children)
        {
            if (axis.Value is not YamlSequenceNode seq)
            {
                throw new InvalidOperationException($"matrix '{axis.Key}' must be a sequence.");
            }

            axes[axis.Key.ToString()] = [.. seq.Select(v => QuoteIfNeeded(v.ToString()))];
        }

        return GenerateCombinations(axes);
    }

    public static IEnumerable<string> ExtractRunSteps(string jobName, YamlMappingNode job)
    {
        if (!job.Children.TryGetValue(new YamlScalarNode("steps"), out var stepsNode) ||
            stepsNode is not YamlSequenceNode steps)
        {
            return [];
        }

        var runs = new List<string>();
        var stepIndex = 0;
        foreach (var step in steps)
        {
            stepIndex++;
            if (step is not YamlMappingNode map)
            {
                continue;
            }

            if (map.Children.ContainsKey(new YamlScalarNode("shell")))
            {
                var stepName = map.Children.TryGetValue(new YamlScalarNode("name"), out var nameNode)
                    ? nameNode.ToString()
                    : $"#{stepIndex}";

                throw new InvalidOperationException($"job '{jobName}' step '{stepName}' specifies shell; custom shells are not supported.");
            }

            if (map.Children.TryGetValue(new YamlScalarNode("run"), out var runNode))
            {
                runs.Add(runNode.ToString());
            }
        }

        return runs;
    }

    public static string BuildCommandScript(Dictionary<string, InputDefinition> inputs, Dictionary<string, YamlMappingNode> jobs, bool useCmdFormatting, bool onceOnly)
    {
        var commands = new List<string>();

        if (useCmdFormatting)
        {
            commands.Add("@ECHO OFF");
            commands.Add(string.Empty);
        }
        else
        {
            commands.Add("set -e");
            commands.Add(string.Empty);
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
                    commands.Add($"echo unsupported runner: {runnerName}");
                    continue;
                }

                foreach (var run in runSteps)
                {
                    var command = run;
                    command = ReplaceSleepCommands(command, useCmdFormatting);
                    command = ReplacePlaceholders(command, inputs, combo, useCmdFormatting);
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

        return string.Join(Environment.NewLine, commands);
    }

    public static string ReplacePlaceholders(string run, IReadOnlyDictionary<string, InputDefinition> inputs, IReadOnlyDictionary<string, string> matrix, bool useCmdFormatting)
    {
        var placeholderResolver = CreatePlaceholderResolver(inputs, matrix);

        var lines = run.Split(LineSeparators, StringSplitOptions.None);
        var processed = new List<string>();

        bool lineContinues = false;
        foreach (var line in lines)
        {
            var withoutComment = RegexHelpers.InlineCommentPattern.Replace(line, string.Empty).TrimEnd();
            var trimmedStart = withoutComment.TrimStart();
            if (string.IsNullOrWhiteSpace(trimmedStart))
            {
                continue;
            }

            if (trimmedStart.StartsWith('#'))
            {
                continue;
            }

            var replacedLine = RegexHelpers.PlaceholderPattern.Replace(withoutComment, new MatchEvaluator(placeholderResolver));
            replacedLine = RegexHelpers.StepSummaryRedirectPattern.Replace(replacedLine, string.Empty).TrimEnd();

            if (useCmdFormatting)
            {
                replacedLine = RegexHelpers.DollarPositionalPattern.Replace(replacedLine, "%$1");
            }

            if (string.IsNullOrWhiteSpace(replacedLine))
            {
                continue;
            }

            if (useCmdFormatting)
            {
                replacedLine = ReplaceTrailingBackslash(replacedLine);

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
                    throw new InvalidOperationException("unsupported template expression found.");
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

    private static bool TryGetMapping(YamlMappingNode parent, string key, out YamlMappingNode mapping)
    {
        mapping = default!;

        if (parent.Children.TryGetValue(new YamlScalarNode(key), out var child) && child is YamlMappingNode node)
        {
            mapping = node;
            return true;
        }

        return false;
    }

    private static string DescribeMatrix(Dictionary<string, string> combo)
    {
        if (combo.Count == 0)
        {
            return "no matrix";
        }

        return string.Join(" ", combo.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private static (bool IsSupported, string RunnerName) ValidateRunsOn(string jobName, YamlMappingNode job, IReadOnlyDictionary<string, string> matrix, IReadOnlyDictionary<string, InputDefinition> inputs)
    {
        var runner = ResolveRunsOn(jobName, job, matrix, inputs);
        return (runner.Equals("ubuntu-latest", StringComparison.OrdinalIgnoreCase), runner);
    }

    private static string ResolveRunsOn(string jobName, YamlMappingNode job, IReadOnlyDictionary<string, string> matrix, IReadOnlyDictionary<string, InputDefinition> inputs)
    {
        if (!job.Children.TryGetValue(new YamlScalarNode("runs-on"), out var runsOnNode))
        {
            throw new InvalidOperationException($"job '{jobName}' must specify runs-on.");
        }

        var placeholderResolver = CreatePlaceholderResolver(inputs, matrix);

        string ReplaceRunnerValue(string value)
        {
            return RegexHelpers.PlaceholderPattern.Replace(value ?? string.Empty, new MatchEvaluator(placeholderResolver)).Trim();
        }

        return runsOnNode switch
        {
            YamlScalarNode scalar => ReplaceRunnerValue(scalar.Value ?? string.Empty),
            YamlSequenceNode sequence => string.Join(" ", sequence.Children
                .Select(node =>
                {
                    if (node is not YamlScalarNode scalarNode)
                    {
                        throw new InvalidOperationException($"job '{jobName}' runs-on entries must be scalars.");
                    }

                    return ReplaceRunnerValue(scalarNode.Value ?? string.Empty);
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            _ => throw new InvalidOperationException($"job '{jobName}' has invalid runs-on definition.")
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
                    throw new InvalidOperationException($"input '{key}' not defined.");
                }

                if (!input.HasDefault)
                {
                    throw new InvalidOperationException($"input '{key}' has no default value.");
                }

                return input.DefaultValue ?? string.Empty;
            }

            if (!matrix.TryGetValue(key, out var matrixValue))
            {
                throw new InvalidOperationException($"matrix value '{key}' not defined.");
            }

            return matrixValue;
        };

    private static string ReplaceSleepCommands(string run, bool useCmdFormatting)
    {
        if (!useCmdFormatting)
        {
            return run;
        }

        return RegexHelpers.SleepCommandPattern.Replace(run, "TIMEOUT /T $1 /NOBREAK >nul");
    }

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
