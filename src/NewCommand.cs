using System;
using System.IO;
using System.Text;

namespace GitHubWorkflow;

internal sealed class NewCommand
{
    public static int Run(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Failed to resolve target directory.");
            }

            Directory.CreateDirectory(directory);

            if (File.Exists(path))
            {
                Console.WriteLine($"Workflow '{Path.GetFileName(path)}' already exists. Skipping creation.");
                return 0;
            }

            var jobName = Path.GetFileNameWithoutExtension(path);

            // NOTE: at least one entry required for 'on'.
            //       (if not, it unexpectedly runs on pull request or etc.)
            File.WriteAllText(path,
$@"name: {jobName}

on:
  #push:
  #  branches: [ ""main"" ]
  #pull_request:
  #  branches: [ ""main"" ]
  #workflow_call:
  #workflow_dispatch:

jobs:

  {jobName}:
    #strategy:
    #  matrix:
    #    configuration: [Debug, Release]

    runs-on: ubuntu-latest   # tip: runs-on can use ${{{{ matrix.<name> }}}}

    steps:
      - uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5      # v4.3.1
      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9  # v4.3.1
        with:
          dotnet-version: 10.x.x

      - run: |
          echo Template created with 'ghx'
"
            );

            Console.WriteLine($"Created workflow template at '{path}'");
            return 0;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create workflow: {ex.Message}", ex);
        }
    }
}
