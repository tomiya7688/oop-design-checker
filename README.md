# oop-design-checker

> **この日本語版が正本です。** 英語互換版は [README.en.md](README.en.md) を参照してください。内容に差異がある場合は、この日本語版を優先します。

オブジェクト指向を採用するプロジェクトが、このリポジトリで定義したオブジェクト指向設計ルールに従っているかを静的解析するツールです。

チェッカー本体は C# で実装し、原則としてルール抑制を使わず、自分自身のルールをすべて通過することを品質条件とします。

## フロントエンド

同じ解析エンジンを、同列のフロントエンドから利用します。

- `src/frontends/cui` - CI 向けコマンドラインフロントエンド
- `src/frontends/gui` - クロスプラットフォーム Avalonia GUI

互換性のため、従来の `src/OopDesignChecker` 実行形式も残します。

## 診断レベル

- `DANGER`（危険）- 本プロジェクトが OOP の根本として扱うルールと両立しない状態。既定では CI を失敗させます。
- `WARN`（警告）- OOP 設計として通常は好ましくない、または疑わしい状態。ただし文脈によって正当化される余地があります。
- `ATTN`（注意）- 厳密な設計上の助言。性能、柔軟性、フレームワーク制約などのトレードオフによって許容される場合があります。

チェッカー自身は `--fail-on attention` で自己検査するため、3レベルすべてで自己違反ゼロを要求します。

## 現在の実装

初期実装は Roslyn と MSBuild を利用した C# 解析です。`.csproj`、`.sln`、`.slnx`、複数の C# プロジェクトを含むディレクトリを解析するときは、各プロジェクト本来のコンパイルを保持します。無関係なプロジェクトを1つの人工的なコンパイルへ混在させません。

実装済みルールは [チェックルール仕様](specification/check-rules.md) に定義します。カプセル化、継承、ポリモーフィズム、可視性、static 状態、処理複雑度、オブジェクト整合性、依存関係、オブジェクト内部ナビゲーションなどを対象とします。

## 品質ゲート

GitHub Actions では、失敗理由を分離して確認できるようにします。

- `normal-ci` - Windows / Linux / macOS で restore、build、ルール smoke test
- `self-check` - チェッカー自身の `src` を `--fail-on attention` で厳格検査
- `code-analyzers` - .NET SDK analyzer を `latest-recommended`、code style 有効、warning-as-error で実行
- `csharpier` - リポジトリローカルの CSharpier 1.3.0 で整形検査
- `release` - リリース関連変更時に Windows / Linux / macOS 向け CUI 配布物を検証

## CUI

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path>
```

`<target-path>` には単独の `.cs`、`.csproj`、`.sln`、`.slnx`、またはディレクトリを指定できます。複数 C# プロジェクトを含むディレクトリはプロジェクト集合として解析し、各プロジェクトは独立した MSBuild compilation を維持したまま、診断だけを集約・重複除去します。

製品バージョンの表示:

```bash
oop-design-checker-cui --version
```

短縮形は `-V` です。

表示言語は `--language ja|en` で選択します。**既定は日本語**で、`en` は英語互換表示です。

CI 失敗しきい値の指定:

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on danger
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on warning
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on attention
```

`--warnings-as-errors` は `--fail-on warning` の互換aliasとして残します。

### リリースパッケージ

`v0.1.0` のような宣言バージョンと一致するタグを push すると、全プラットフォームのパッケージ作成成功後に GitHub Release を自動生成します。製品バージョンは `Directory.Build.props` の `VersionPrefix` を単一の正本とし、`v*` タグと一致しなければリリース処理を失敗させます。

配布archiveには `dotnet publish` の完全な出力、日本語正本と英語互換版の README / CHANGELOG、`oop-design-checker.example.json` を含めます。archive名には製品バージョンを含め、例として `oop-design-checker-0.1.0-linux-x64.tar.gz` とします。各archiveには `.sha256` を付与し、タグリリースでは `SHA256SUMS.txt` も生成します。

配布対象:

- `win-x64` / `win-arm64`: `.zip`
- `linux-x64` / `linux-arm64`: `.tar.gz`
- `osx-x64` / `osx-arm64`: `.tar.gz`

パッケージは self-contained なので、CUI を起動するだけなら対象 .NET runtime の事前インストールは不要です。ただし `.csproj`、`.sln`、`.slnx` は MSBuild 経由で読むため、互換性のある .NET SDK / MSBuild が検出可能である必要があります。単独 `.cs` 解析ではプロジェクト読み込みは不要です。

展開後の例:

```bash
./oop-design-checker-cui <target-path> --fail-on danger
```

Windows では `oop-design-checker-cui.exe` を使用します。

### リリース手順

1. `Directory.Build.props` の `VersionPrefix` を更新する。
2. 正本の `CHANGELOG.md` に日本語でリリース内容を記録し、その後 `CHANGELOG.en.md` を追従更新する。
3. normal CI、strict self-check、Analyzer、CSharpier、6 RID のrelease packaging matrixがすべて緑であることを確認してマージする。
4. `v0.1.0` のように、宣言バージョンと完全一致する `v` prefixタグをpushする。
5. release workflowがタグとバージョンの一致を再検証し、6パッケージ、SHA-256、GitHub Releaseを生成する。

### CI向け出力形式

既定の `text` は人が読むためのローカライズ済み短文出力です。機械可読形式は `--format` で選択します。

```bash
# 安定したJSON文書
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format json --output oop-diagnostics.json

# code scanning向けSARIF 2.1.0
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format sarif --output oop-diagnostics.sarif

# GitHub Actions workflow annotation
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format github
```

対応形式:

- `text` - ローカライズされた通常診断 + summary
- `json` - version付き診断配列とseverity summary。schema互換性を優先します。
- `sarif` - rule metadata、severity、source location、symbol metadataを含むSARIF 2.1.0
- `github` - `::notice` / `::warning` / `::error` workflow command

`--output <file>` は `text`、`json`、`sarif` で利用できます。GitHub annotationは意図的に常にstdoutへ出します。出力形式はprocess exit codeに影響せず、CI失敗条件は常に `--fail-on` が決定します。

## 設定

既定では、解析対象ディレクトリから親方向へたどり、最も近い `oop-design-checker.json` を探索します。これにより、リポジトリ/solution rootに1つ設定を置いた状態でも、MSBuildやCIがネストした `.csproj` を直接対象にしたとき同じ設定を適用できます。複数ある場合は対象に最も近いものを採用します。

別の設定は `--config <path>` で指定できます。絶対パスはそのまま使用します。相対パスは既存互換のためprocessのcurrent working directoryを先に確認し、見つからなければ解析対象ディレクトリ基準でも確認します。明示指定した設定が存在しない場合は、既定値へ黙ってfallbackせずエラーにします。

```json
{
  "ignoredPaths": [
    "**/Generated/**",
    "**/*.g.cs"
  ],
  "disabledRules": [
    "OOP103"
  ],
  "failureThreshold": "danger",
  "ruleSettings": {
    "oop304": {
      "warningDepth": 4
    }
  }
}
```

- `ignoredPaths` - 一致するファイル/ディレクトリを解析対象外にする。
- `disabledRules` - 指定したrule IDをその解析で完全に抑制する。
- `failureThreshold` - processを失敗扱いにする診断レベルを指定する。
- `ruleSettings.oop304.warningDepth` - OOP304を出すプロジェクト所有のclass inheritance depth。既定は `4`、最小 `1`。同じ解析へ読み込まれた別プロジェクトのbase typeは数えますが、外部framework/library ancestryは数えません。

コピー可能な例は `oop-design-checker.example.json` を参照してください。

## GUI

```bash
dotnet run --project src/frontends/gui/OopDesignChecker.Gui.csproj
```

GUIも**日本語を既定**とし、英語互換表示へ切り替えられます。解析対象pathと任意の設定pathを指定し、CUIと同じ解析結果を表示します。

## ビルド

対象frameworkは .NET 10 です。共有エンジン、CUI、GUIをWindows / Linux / macOSでクロスプラットフォームビルドします。

CUI publish例:

```bash
dotnet publish src/frontends/cui/OopDesignChecker.Cui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

必要に応じて `win-x64` を `win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64` に置き換えます。Roslyn/MSBuild補助ファイルを欠落させないため、配布時はpublish directory全体を保持します。

## ドキュメント

日本語版を正本とします。

- [変更履歴（正本）](CHANGELOG.md)
- [オブジェクト指向設計定義（正本）](specification/object-oriented-design.md)
- [チェックルール仕様（正本）](specification/check-rules.md)
- [English compatibility README](README.en.md)
- [English compatibility changelog](CHANGELOG.en.md)
- [English compatibility design definition](specification/en/object-oriented-design.md)
- [English compatibility rule reference](specification/en/check-rules.md)
