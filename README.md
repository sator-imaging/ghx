[![nuget](https://img.shields.io/nuget/vpre/GitHubWorkflow)](https://www.nuget.org/packages/GitHubWorkflow)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/GitHubWorkflow)

[🇺🇸 English](./README.md)
&nbsp; ❘ &nbsp;
[🇯🇵 日本語版](./README.ja.md)
&nbsp; ❘ &nbsp;
[🇨🇳 简体中文版](./README.zh-CN.md)



`ghx` runs GitHub Actions workflow as a batch script on Windows, macOS and Linux.





# ✨ Key Features

- **Matrix Expansion:** preview and run every combo or keep only the first for speed.
- **Bash to CMD Conversion:** Windows or bash, no tweaks needed.





# 🚀 Usage

## ⚡ Instant Execution (`dnx`)

```bash
dnx GitHubWorkflow -- dry my-workflow
```


## 📦 Install as a Tool (`ghx`)

```bash
dotnet tool install -g GitHubWorkflow
```

Run by `ghx`: GitHub workflow eXecute

```bash
ghx new my-workflow     # create new workflow
ghx dry my-workflow     # dry run (prints generated script)
ghx my-workflow --once  # run only the first matrix combination
```





# ⚙️ Command Line Options

**Synopsis:**

```bash
ghx [command] [options] <workflow-file>
```

## Commands
- `run`: writes a temp script file and execute. (default)
- `dry`: prints run steps
- `new`: creates an empty workflow file under `.github/workflows`

## Options
- `--cmd`: emit Windows `cmd.exe`-formatted output (default on Windows only).
- `--wsl`: force bash-compatible output; conflicts with `--cmd`.
- `--once`/`-1`: keep only the first matrix combination per job (skips the rest).
- `workflow-file`: required, must be the file name only (no paths). Resolves to `.github/workflows/<name>.yml|.yaml` relative to the current directory.





# 🧭 Common Usecase

Create a new workflow:

```bash
ghx new test   # creates .github/workflows/test.yml
```


Edit template:

```yaml
on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]
  workflow_call:
  workflow_dispatch:

jobs:
  test:

    # Basic bash-to-cmd conversion is supported
    # See Technical Notes for further information
    runs-on: ubuntu-latest

    # Matrix expansion is supported
    strategy:
      matrix:
        configuration: [Debug, Release]

    # 'uses' are completely ignored
    steps:
    - uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5      # v4.3.1
    - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9  # v4.3.1
      with:
        dotnet-version: 10.x.x

    # Collects all 'run' steps from workflow file
    - name: Test
      run: |
        dotnet restore ./src
        dotnet build ./src \
          --no-restore \
          -c ${{ matrix.configuration }}

  # Multiple jobs are supported
  other_job:
   ...
```


From a repo root with `.github/workflows/test.yml` present, quickly inspect what will run:

```bash
ghx dry test
```


Trial-run the same workflow locally with matrix expansion collapsed to a single combination:

```bash
ghx run test --once
```


On Windows, force `cmd.exe` formatting or override it to emit bash-compatible scripts:

```bash
ghx run test --cmd    # default on Windows
ghx run test --wsl    # force bash via WSL/bash even on Windows
```



## 🧩 Composite Actions

Here shows GitHub Actions composite sample that uses reusable `test` workflow.

```yaml
name: ci

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]
  workflow_dispatch:

jobs:

  test:
    uses: ./.github/workflows/test.yml    # 👈👈👈

  # Subsequent job depending on 'test' result
  build:
    needs: test
    if: ${{ !failure() }}
    runs-on: ubuntu-latest

    steps:
    - uses: ...
```

> [!TIP]
> Reusable workflow cannot be used in conjunction with `steps`.





# 📝 Technical Notes

- Placeholder values for `inputs.*` and `matrix.*` are pulled from defaults/matrix entries and quoted as needed.
- Any `>> $GITHUB_STEP_SUMMARY` or `> $GITHUB_STEP_SUMMARY` redirections are removed from generated commands.
- Inline comments (`# ...`) in `run` steps are stripped before processing.
- Steps cannot specify a custom `shell`; the tool only supports the default shell for the selected output format and will error if a step sets `shell`.
- Bash-to-cmd conversion replaces trailing `\` with `^`, prepends `CALL` (since many tools ship as `.bat`/`.cmd`), and appends a failure guard to mimic `bash -e` behavior on Windows.
- `sleep <n>` lines inside run blocks become `TIMEOUT /T <n> /NOBREAK >nul` in cmd output; only integer durations are supported.
- During cmd conversion, positional placeholders `$0`–`$9` in `run` steps become `%0`–`%9` (only single-digit positions are supported).
- Workflow `inputs.*` may omit defaults, but if a run step references one without a default, the tool fails fast.
- Multiple jobs are supported, but they share process state; no environment reset happens between jobs.
- Jobs are executed sequentially; a job starts only after all matrix combinations from the previous job finish.
- Expands matrix combinations (optional `--once` to keep only the first).
- Matrix handling is basic: only simple axes arrays are supported (no `include`/`exclude`/`fail-fast`/`max-parallel` or nested objects).
- Generates `bash` or Windows `cmd`-formatted scripts.





# ⏳ Missing Features

TODO

- `-i/--input key=value`: Ability to override `inputs` of `workflow_call` (allow multiple)
- `--step-summary <path>`: Instead of removing redirections, set custom output path for.
- `$*` and `$@` conversion: `%*` exists in cmd but it is not complete equivalent. (`"$@"` is equivalent to `%*`; quote required)
- `runs-on`: Due to bash to cmd conversion is only supported, it doesn't accept rather than `ubuntu-latest`.
- Native AOT Support: Some report found that `VYaml` can be compiled with Native AOT enabled.
