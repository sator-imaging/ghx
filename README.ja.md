[![nuget](https://img.shields.io/nuget/vpre/GitHubWorkflow)](https://www.nuget.org/packages/GitHubWorkflow)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/GitHubWorkflow)

[🇺🇸 English](./README.md)
&nbsp; ❘ &nbsp;
[🇯🇵 日本語版](./README.ja.md)
&nbsp; ❘ &nbsp;
[🇨🇳 简体中文版](./README.zh-CN.md)



`ghx` は GitHub Actions のワークフローを Windows / macOS / Linux 上でバッチスクリプトとして実行します。





# ✨ 主要機能

- **マトリクス展開:** すべての組み合わせを事前確認・実行、または高速化のために先頭のみ実行。
- **Bash→CMD 変換:** Windows でも bash でも設定不要ですぐ動作。





# 🚀 使い方

## ⚡ 即時実行 (`dnx`)

```bash
dnx GitHubWorkflow -- dry my-workflow
```


## 📦 ツールとしてインストール (`ghx`)

```bash
dotnet tool install -g GitHubWorkflow
```

`ghx` で実行: GitHub workflow eXecute

```bash
ghx new my-workflow     # ワークフローを新規作成
ghx dry my-workflow     # ドライラン (生成スクリプトを表示)
ghx my-workflow --once  # マトリクスの先頭組み合わせだけ実行
```





# ⚙️ コマンドラインオプション

**書式:**

```bash
ghx [command] [options] <workflow-file>
```

## Commands
- `run`: 一時スクリプトを書き出して実行 (デフォルト)
- `dry`: 実行手順を出力
- `new`: `.github/workflows` 配下に空のワークフロー ファイルを作成

## Options
- `--cmd`: Windows の `cmd.exe` 形式で出力 (Windows ではデフォルト)。
- `--wsl`: bash 互換で出力を強制。`--cmd` と併用不可。
- `--once`/`-1`: ジョブごとにマトリクスの先頭組み合わせだけ残し、残りをスキップ。
- `workflow-file`: 必須。ファイル名のみ (パス不可)。カレントディレクトリから `.github/workflows/<name>.yml|.yaml` に解決します。





# 🧭 よくあるユースケース

新しいワークフローを作成:

```bash
ghx new test   # .github/workflows/test.yml を作成
```


テンプレートを編集:

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

    # 基本的な bash→cmd 変換をサポート
    # 詳細は Technical Notes を参照
    runs-on: ubuntu-latest

    # マトリクス展開をサポート
    strategy:
      matrix:
        configuration: [Debug, Release]

    # 'uses' は完全に無視
    steps:
    - uses: actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5      # v4.3.1
    - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9  # v4.3.1
      with:
        dotnet-version: 10.x.x

    # ワークフローからすべての 'run' ステップを収集
    - name: Test
      run: |
        dotnet restore ./src
        dotnet build ./src \
          --no-restore \
          -c ${{ matrix.configuration }}

  # 複数ジョブをサポート
  other_job:
   ...
```

リポジトリのルートに `.github/workflows/test.yml` がある状態で、実行内容をすぐ確認:

```bash
ghx dry test
```

同じワークフローをローカルで試し、マトリクスを 1 組だけに圧縮して実行:

```bash
ghx run test --once
```

Windows で `cmd.exe` 形式を強制したり、bash 互換出力に切り替えたり:

```bash
ghx run test --cmd    # Windows ではデフォルト
ghx run test --wsl    # Windows でも WSL/bash を強制
```



## 🧩 Composite Actions

再利用可能な `test` ワークフローを呼び出す GitHub Actions composite サンプルです。

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

  # 'test' の結果に依存する後続ジョブ
  build:
    needs: test
    if: ${{ !failure() }}
    runs-on: ubuntu-latest

    steps:
    - uses: ...
```

> [!TIP]
> 再利用可能ワークフローは `steps` と同時には使えません。





# 📝 Technical Notes

- `inputs.*` と `matrix.*` のプレースホルダーは defaults/matrix の値を使い、必要に応じてクオートします。
- 生成コマンドから `>> $GITHUB_STEP_SUMMARY` および `> $GITHUB_STEP_SUMMARY` のリダイレクトを削除します。
- `run` ステップ内のインラインコメント (`# ...`) は処理前に除去します。
- ステップでカスタム `shell` は指定できません。選択した出力形式のデフォルトシェルのみをサポートし、`shell` が設定されているとエラーになります。
- Bash→cmd 変換では末尾の `\` を `^` に置換し、多くのツールが `.bat`/`.cmd` であることを踏まえて `CALL` を付与し、Windows での `bash -e` 相当の失敗ガードを追加します。
- `run` ブロック内の `sleep <n>` は cmd 出力時に `TIMEOUT /T <n> /NOBREAK >nul` に置換されます (整数のみ対応)。
- cmd 変換中、`run` ステップ内の位置プレースホルダー `$0`–`$9` は `%0`–`%9` に置換されます (1 桁のみ対応)。
- ワークフローの `inputs.*` がデフォルトを持たなくても構いませんが、デフォルトなしの入力を `run` ステップが参照するとツールは即時失敗します。
- 複数ジョブをサポートしますが、プロセス状態は共有されます。ジョブ間で環境リセットは行いません。
- マトリクス組み合わせを展開します (必要に応じて `--once` で先頭のみ残せます)。
- マトリクス処理はシンプルです。単純な軸配列のみ対応で、`include`/`exclude`/`fail-fast`/`max-parallel` やネストオブジェクトには非対応です。
- `bash` または Windows `cmd` 形式のスクリプトを生成します。





# ⏳ Missing Features

TODO

- `-i/--input key=value`: `workflow_call` の `inputs` を上書きできるようにする (複数可)
- `--step-summary <path>`: リダイレクトを削除する代わりにカスタム出力パスを設定。
- `$*` と `$@` の変換: cmd には `%*` があるが完全互換ではない (`"$@"` は `%*` が近い。クオート必須)。
- `runs-on`: bash→cmd 変換のみ対応のため、`ubuntu-latest` 以外は受け付けない。
- Native AOT Support: 一部報告では `VYaml` を Native AOT 有効でコンパイルできています。
