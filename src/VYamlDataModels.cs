#if __TODO_VYAML_MIGRATION__

using System.Collections.Generic;
using VYaml.Annotations;

namespace GitHubWorkflow;

// TODO: VYaml migration touches WorkflowUtilities methods:
// - LoadRoot
// - ParseInputs
// - GetJobs
// - BuildMatrix
// - ExtractRunSteps
// - ResolveRunsOn / ValidateRunsOn
// - TryGetMapping
// - Any signatures using Yaml* nodes

// Minimal POCOs covering only the YAML shapes the current implementation reads.
[YamlObject]
internal sealed class WorkflowRoot
{
    [YamlMember("on")]
    public WorkflowOn? On { get; init; }

    // Jobs are keyed by arbitrary job name.
    public Dictionary<string, JobDefinition>? Jobs { get; init; }
}

[YamlObject]
internal sealed class WorkflowOn
{
    [YamlMember("workflow_call")]
    public WorkflowCall? WorkflowCall { get; init; }
}

[YamlObject]
internal sealed class WorkflowCall
{
    public Dictionary<string, WorkflowInput>? Inputs { get; init; }
}

[YamlObject]
internal sealed class WorkflowInput
{
    // Only the default value is used in the current logic.
    [YamlMember("default")]
    public string? Default { get; init; }
}

[YamlObject]
internal sealed class JobDefinition
{
    // Accepts scalar or sequence; a converter/normalizer will be added when wiring VYaml.
    [YamlMember("runs-on")]
    public object? RunsOn { get; init; }

    public JobStrategy? Strategy { get; init; }

    public List<JobStep>? Steps { get; init; }
}

[YamlObject]
internal sealed class JobStrategy
{
    // Matrix axes are sequences of strings in the current workflow parsing code.
    public Dictionary<string, List<string>>? Matrix { get; init; }
}

[YamlObject]
internal sealed class JobStep
{
    public string? Name { get; init; }

    public string? Run { get; init; }

    public string? Shell { get; init; }
}

#endif
