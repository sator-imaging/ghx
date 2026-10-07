$workflowDirectory = Join-Path $PWD ".github/workflows"
$fixturesDirectory = Join-Path $PWD ".github/test-fixtures"
$dollarWorkflow = "dry-shell-dollar"
$templateWorkflow = "dry-shell-template"

Copy-Item (Join-Path $fixturesDirectory "$dollarWorkflow.yml") (Join-Path $workflowDirectory "$dollarWorkflow.yml") -Force
Copy-Item (Join-Path $fixturesDirectory "$templateWorkflow.yml") (Join-Path $workflowDirectory "$templateWorkflow.yml") -Force

function Invoke-Dry([string]$workflowName) {
    $output = & dotnet run -c Debug -f net10.0 --project ./src -- dry --wsl $workflowName 2>&1
    return @{
        ExitCode = $LASTEXITCODE
        Output = $output | Out-String
    }
}

try {
    if ($env:RUNNER_OS -eq "Windows") {
        $result = Invoke-Dry $dollarWorkflow
        if ($result.ExitCode -eq 0 -or $result.Output -notmatch "Unsupported template expression found:") {
            throw "Expected dry to reject a shell `$` variable on Windows. Output: $($result.Output)"
        }
    }
    else {
        $result = Invoke-Dry $dollarWorkflow
        if ($result.ExitCode -ne 0 -or $result.Output -notmatch 'echo "\$HOME"') {
            throw "Expected dry to allow a shell `$` variable on Linux. Output: $($result.Output)"
        }

        $result = Invoke-Dry $templateWorkflow
        if ($result.ExitCode -eq 0 -or $result.Output -notmatch "Unsupported template expression found after script processing:") {
            throw "Expected dry to reject an unresolved `${{` expression on Linux. Output: $($result.Output)"
        }
    }
}
finally {
    Remove-Item (Join-Path $workflowDirectory "$dollarWorkflow.yml") -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $workflowDirectory "$templateWorkflow.yml") -ErrorAction SilentlyContinue
}
