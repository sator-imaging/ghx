using GitHubWorkflow;
using System;
using System.CommandLine;

var workflowFileArgument = new Argument<string>("workflow-file")
{
    Description = "GitHub workflow file name (.yml/.yaml or base name).",
    CustomParser = ArgumentHelpers.ParseWorkflowPath,
};

var cmdFlag = new Option<bool>("--cmd")
{
    Description = "Emit commands formatted for Windows cmd.exe (on by default on Windows; prepend @echo off, replace trailing backslash with ^, etc).",
    DefaultValueFactory = _ => OperatingSystem.IsWindows(),
};

var wslFlag = new Option<bool>("--wsl")
{
    Description = "Force bash-compatible output (disables --cmd even on Windows).",
};

var onceFlag = new Option<bool>("--once", "-1")
{
    Description = "Use only the first matrix combination per job.",
};


var dryCommand = new Command("dry", "Prints run steps for a workflow. [Default]")
{
    cmdFlag,
    wslFlag,
    onceFlag,
    workflowFileArgument,
};

var runCommand = new Command("run", "Generates a runnable script for a workflow and prints the command to execute it.")
{
    cmdFlag,
    wslFlag,
    onceFlag,
    workflowFileArgument,
};

var newCommand = new Command("new", "Creates a new workflow template under .github/workflows.")
{
    workflowFileArgument,
};

var rootCommand = new RootCommand("GitHub Actions workflow tool.")
{
    // works as 'run' by default
    cmdFlag,
    wslFlag,
    onceFlag,
    workflowFileArgument,

    // sub commands
    dryCommand,
    runCommand,
    newCommand,
};

var dryAction = (ParseResult args) =>
{
    return ConsoleHelpers.RunWithErrorHandling(rootCommand, () =>
    {
        var resolvedPath = args.GetRequiredValue(workflowFileArgument);
        var useCmd = args.GetValue(cmdFlag);
        var useWsl = args.GetValue(wslFlag);
        var onceOnly = args.GetValue(onceFlag);

        var useCmdFormatting = useCmd && !useWsl;

        WorkflowFileHelpers.EnsureWorkflowExists(resolvedPath);

        DryCommand.Run(resolvedPath, useCmdFormatting, onceOnly);
        return 0;
    });
};

var runAction = (ParseResult args) =>
{
    return ConsoleHelpers.RunWithErrorHandling(rootCommand, () =>
    {
        var resolvedPath = args.GetRequiredValue(workflowFileArgument);
        var useCmd = args.GetValue(cmdFlag);
        var useWsl = args.GetValue(wslFlag);
        var onceOnly = args.GetValue(onceFlag);

        var useCmdFormatting = useCmd && !useWsl;

        WorkflowFileHelpers.EnsureWorkflowExists(resolvedPath);

        return RunCommand.Run(resolvedPath, useCmdFormatting, useWsl, onceOnly);
    });
};

var newAction = (ParseResult args) =>
{
    return ConsoleHelpers.RunWithErrorHandling(rootCommand, () =>
    {
        var targetPath = args.GetRequiredValue(workflowFileArgument);

        return NewCommand.Run(targetPath);
    });
};

dryCommand.SetAction(dryAction);
runCommand.SetAction(runAction);
newCommand.SetAction(newAction);
rootCommand.SetAction(runAction);

dryCommand.Validators.Add(ArgumentHelpers.ValidateCmdVsWsl(cmdFlag, wslFlag));
runCommand.Validators.Add(ArgumentHelpers.ValidateCmdVsWsl(cmdFlag, wslFlag));
rootCommand.Validators.Add(ArgumentHelpers.ValidateCmdVsWsl(cmdFlag, wslFlag));

return await rootCommand.Parse(args).InvokeAsync();
