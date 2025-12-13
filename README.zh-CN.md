[![nuget](https://img.shields.io/nuget/vpre/ghx?label=ghx)](https://www.nuget.org/packages/ghx)
[![nuget](https://img.shields.io/nuget/vpre/GitHubWorkflow)](https://www.nuget.org/packages/GitHubWorkflow)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/GitHubWorkflow)

[🇺🇸 English](./README.md)
&nbsp; ❘ &nbsp;
[🇯🇵 日本語版](./README.ja.md)
&nbsp; ❘ &nbsp;
[🇨🇳 简体中文版](./README.zh-CN.md)



`ghx` 在 Windows、macOS、Linux 上将 GitHub Actions 工作流作为批处理脚本执行。





# ✨ 关键特性

- **矩阵展开：** 预览并运行全部组合，或为加速仅保留第一个组合。
- **Bash→CMD 转换：** 无论 Windows 还是 bash，开箱即用无需修改。





# 🚀 使用方式

## ⚡ 即刻执行 (`dnx`)

```bash
dnx ghx -- dry my-workflow
```


## 📦 安装为工具 (`ghx`)

```bash
dotnet tool install -g ghx
```

通过 `ghx` 运行：GitHub workflow eXecute

```bash
ghx new my-workflow     # 新建一个工作流
ghx dry my-workflow     # 干跑 (打印生成的脚本)
ghx my-workflow --once  # 仅运行矩阵的首个组合
```





# ⚙️ 命令行选项

**用法:**

```bash
ghx [command] [options] <workflow-file>
```

## Commands
- `run`: 写入临时脚本文件并执行 (默认)
- `dry`: 打印运行步骤
- `new`: 在 `.github/workflows` 下创建工作流文件 (如有 `.github/ghx_template.yml|.yaml` 则使用该模板)

## Options
- `--cmd`: 输出 Windows `cmd.exe` 格式 (仅 Windows 上为默认；在 macOS/Linux 上仅用于 dry 预览)。
- `--wsl`: 强制输出 bash 兼容格式；与 `--cmd` 冲突。
- `--once`/`-1`: 每个作业仅保留矩阵的首个组合 (跳过其余)。
- `workflow-file`: 必填，仅文件名 (不含路径)。从当前目录解析为 `.github/workflows/<name>.yml|.yaml`。





# 🧭 常见用例

新建一个工作流：

```bash
ghx new test   # 创建 .github/workflows/test.yml
```

> [!TIP]
> 如果仓库根目录存在 `.github/ghx_template.yml` 或 `.github/ghx_template.yaml`，将直接复制该文件；否则会使用默认模板。


编辑模板：

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

    # 支持基础的 bash→cmd 转换
    # 详细说明见 Technical Notes
    runs-on: ubuntu-latest

    # 支持矩阵展开
    strategy:
      matrix:
        configuration: [Debug, Release]

    # 'uses' 完全忽略
    steps:
      - uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5      # v4.3.1
      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9  # v4.3.1
        with:
          dotnet-version: 10.x.x

      # 从工作流文件收集所有 'run' 步骤
      - name: Test
        run: |
          dotnet restore ./src
          dotnet build ./src \
            --no-restore \
            -c ${{ matrix.configuration }}

  # 支持多个作业
  other_job:
   ...
```

在仓库根目录已有 `.github/workflows/test.yml` 时，快速查看将要执行的内容：

```bash
ghx dry test
```

以单一矩阵组合在本地试跑相同工作流：

```bash
ghx run test --once
```

在 Windows 上强制 `cmd.exe` 格式或改为 bash 兼容输出：

```bash
ghx run test --cmd    # Windows 默认
ghx run test --wsl    # 即使在 Windows 也强制 WSL/bash
```



## 🧩 Composite Actions

下面是一个调用可复用 `test` 工作流的 GitHub Actions 组合示例。

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

  # 依赖于 'test' 结果的后续作业
  build:
    needs: test
    if: ${{ !failure() }}
    runs-on: ubuntu-latest

    steps:
    - uses: ...
```

> [!TIP]
> 可复用工作流不能与 `steps` 同时使用。





# 📝 支持功能概览

| 功能                  | 支持级别 | 备注                                                        |
|-----------------------|----------|-------------------------------------------------------------|
| `workflow_call` 触发器  | ✅ 完全    | 主要用途；`workflow_dispatch` 也可用                        |
| 输入定义              | ✅ 完全    | 类型声明被忽略；引用时必须有默认值                          |
| 矩阵策略              | ✅ 完全    | 笛卡尔展开；可用 `--once` 只取第一个                        |
| 多个作业              | ✅ 完全    | 顺序执行；无并行、无环境隔离                                |
| Run 步骤              | ✅ 完全    | 仅提取 `run:`；`uses:` 被忽略                               |
| 占位符表达式          | ⚠️ 部分    | 仅支持 `${{ inputs.* }}` 与 `${{ matrix.* }}`                |
| Bash 脚本             | ✅ 完全    | 默认 shell；通过转换跨平台                                  |
| 自定义 shell          | ❌ 无      | 指定 `shell:` 会报错                                        |
| Runner                | ⚠️ 有限    | 只执行 `ubuntu-latest`，其他值仅提示警告                    |
| 位置参数              | ⚠️ 有限    | CMD 输出时 `$0-$9` 转为 `%0-%9`                             |
| Sleep 命令            | ✅ 完全    | `sleep N` 会在 Windows 上替换为 `TIMEOUT /T N /NOBREAK >nul` |



## Technical Notes

- `inputs.*` 与 `matrix.*` 的占位符取自 defaults/matrix，并按需加引号。
- 生成的命令会移除 `>> $GITHUB_STEP_SUMMARY` 与 `> $GITHUB_STEP_SUMMARY` 重定向。
- 处理前会剥离 `run` 步骤中的行内注释 (`# ...`)。
- 步骤不可指定自定义 `shell`；仅支持所选输出格式的默认 shell，如指定 `shell` 会报错。
- Bash→cmd 转换会将行末的 `\` 换为 `^`，添加 `CALL` (许多工具为 `.bat`/`.cmd`)，并附加失败保护以模拟 Windows 上的 `bash -e` 行为。
- `run` 块中的 `sleep <n>` 在 cmd 输出中替换为 `TIMEOUT /T <n> /NOBREAK >nul` (仅支持整数时长)。
- 在 cmd 转换中，`run` 步骤的 `$0`–`$9` 位置占位符替换为 `%0`–`%9` (仅支持单个数字)。
- 工作流中的 `inputs.*` 可没有默认值，但若 `run` 步骤引用了无默认值的输入，工具会立即失败。
- 支持多个作业，但会共享进程状态；作业之间不会重置环境。
- 展开矩阵组合 (可通过 `--once` 仅保留第一个)。
- 矩阵支持较为基础：仅支持简单轴数组，不支持 `include`/`exclude`/`fail-fast`/`max-parallel` 或嵌套对象。
- 生成 `bash` 或 Windows `cmd` 格式的脚本。





# ⏳ Missing Features

TODO

- `-i/--input key=value`: 覆盖 `workflow_call` 的 `inputs` (可多次传入)
- `--step-summary <path>`: 不删除重定向，改为设置自定义输出路径。
- `$*` 与 `$@` 的转换: cmd 有 `%*` 但不完全等价 (`"$@"` 相当于 `%*`，需加引号)。
- `runs-on`: 由于仅支持 bash→cmd 转换，除了 `ubuntu-latest` 其余值不接受。
- Native AOT Support: 有报告称 `VYaml` 可以在启用 Native AOT 时编译通过。
