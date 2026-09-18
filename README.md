# oop-design-checker

[English](README.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

オブジェクト指向を採用するプロジェクトが、このリポジトリで定義したオブジェクト指向設計ルールに沿っているかを静的解析するチェッカーです。

チェッカー本体はC#で実装し、原則として自己違反を抑制せず、自分自身のルールを通過することを求めます。

## フロントエンド

同じ解析engineを、兄弟関係のfrontendから利用します。

- `src/frontends/cui` - CI向けcommand-line frontend
- `src/frontends/gui` - cross-platform Avalonia GUI frontend

互換性のため、従来の`src/OopDesignChecker` executableも残しています。

## 診断レベル

- `DANGER` - このprojectが基本原則として扱うOOPルールと両立しない状態。既定でCIを失敗させます。
- `WARN` - OOP設計として通常は望ましくないが、必ずしも致命的ではない状態。
- `ATTN` - 厳格な設計上は注意すべきものの、performance・柔軟性・framework制約などのtrade-offで正当化され得る状態。

チェッカー自身は`--fail-on attention`でself-checkするため、3段階すべてを満たす必要があります。

## 現在の実装

最初の解析backendはC#を対象とし、RoslynとMSBuildを使用しています。`.csproj`、`.sln`、`.slnx`、複数のC# projectを含むdirectoryを解析する場合も、実際のproject compilationを保ちます。無関係なprojectを1つの仮想compilationへ混ぜることはしません。

実装済みruleは[チェックルール仕様](specification/check-rules.md)で定義しています。カプセル化、継承、多態性、可視性、static state、operation complexity、object integrity、dependency、navigationなどを扱います。

## 品質ゲート

GitHub Actionsでは、失敗原因を切り分けやすいよう独立したcheckを用意しています。

- `normal-ci` - Windows / Linux / macOSでrestore、build、rule smoke test
- `self-check` - checker自身の`src`を`--fail-on attention`で解析
- `code-analyzers` - .NET SDK analyzerを`latest-recommended`で実行し、code style有効・warningをerror化
- `csharpier` - repository-local tool manifestのCSharpier 1.3.0でformat check
- `release` - release関連変更時にWindows / Linux / macOS向けCUI packageを検証

## CUI

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path>
```

`<target-path>`には、単体`.cs`、`.csproj`、`.sln`、`.slnx`、directoryを指定できます。複数のC# projectを含むdirectoryでは、各projectのMSBuild compilationを独立して保持したままproject setとして解析し、診断だけを集約・重複除去します。

製品versionの表示:

```bash
oop-design-checker-cui --version
```

短縮形は`-V`です。

CI失敗しきい値の指定:

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on danger
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on warning
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on attention
```

`--warnings-as-errors`は`--fail-on warning`の互換aliasとして残しています。

表示言語の指定:

```bash
oop-design-checker-cui <target-path> --language ja
oop-design-checker-cui <target-path> --language en
```

既定は`ja`です。CUIの`text`出力とGUIの人間向け表示では、rule title・診断message・summaryを選択言語で表示します。`OOP001`等のrule ID、CLI option名、JSON設定keyは変更しません。`json` / `sarif` / `github`出力はdownstream toolとの互換性を保つため、言語選択に影響されないcanonical textを維持します。

### Release package

`v0.1.0`のように宣言versionと一致するtagをpushすると、全platform packageが成功した後にGitHub Releaseを作成します。製品versionは`Directory.Build.props`へ一元化しており、`v*` tagと宣言versionが一致しなければrelease packaging前に失敗します。

release archiveには`dotnet publish`の完全な出力、日英両README、日英両CHANGELOG、`oop-design-checker.example.json`を含めます。archive名にはversionを含め、例として`oop-design-checker-0.1.0-linux-x64.tar.gz`となります。各archiveには`.sha256`を付け、tagged releaseには統合`SHA256SUMS.txt`も含めます。

配布target:

- `win-x64` / `win-arm64`: `.zip`
- `linux-x64` / `linux-arm64`: `.tar.gz`
- `osx-x64` / `osx-arm64`: `.tar.gz`

packageはself-containedなので、CUI起動だけなら対応する.NET runtimeの事前installは不要です。ただし`.csproj`、`.sln`、`.slnx`解析はMSBuild経由で読み込むため、互換性のある.NET SDK/MSBuildが検出可能である必要があります。単体`.cs`解析ではproject loadingは不要です。

展開後の例:

```bash
./oop-design-checker-cui <target-path> --fail-on danger
```

Windowsでは`oop-design-checker-cui.exe`を使用します。

### Release手順

新しいreleaseでは次を行います。

1. `Directory.Build.props`の`VersionPrefix`を更新する。
2. `CHANGELOG.md`へrelease entryを追加する。
3. normal CI、strict self-check、analyzer、CSharpier、6 RID release packagingがすべて緑になってからmergeする。
4. 宣言versionと完全一致する`v` prefix付きtagをpushする。例: `v0.1.0`。
5. release workflowがtag/version一致を検証し、6 packageを再buildし、SHA-256を生成してGitHub Releaseを作成する。

### CI出力形式

既定の`text`形式は短い人間向け表示として維持します。機械可読形式は`--format`で選択できます。

```bash
# 安定したJSON document
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format json --output oop-diagnostics.json

# code scanning向けSARIF 2.1.0
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format sarif --output oop-diagnostics.sarif

# GitHub Actions annotation
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format github
```

対応形式:

- `text` - 通常の`ATTN` / `WARN` / `DANGER`行とsummary
- `json` - version付きdiagnostic arrayとseverity summary
- `sarif` - rule metadata、severity、source location、symbol metadataを含むSARIF 2.1.0
- `github` - `::notice` / `::warning` / `::error` workflow commandを出力し、GitHub Actions annotationとして表示

`--output <file>`は`text`、`json`、`sarif`で利用できます。GitHub annotationは意図的に常にstdoutへ出します。出力形式はprocess exit codeへ影響せず、CI失敗条件は`--fail-on`だけが決めます。

## 設定

既定では、target directoryから親directoryへ順に`oop-design-checker.json`を探索し、最も近い設定を使用します。これにより、MSBuildやCIがnested `.csproj`を直接解析する場合でも、repository/solution rootの設定を各projectやbuild outputへ複製せず適用できます。複数存在する場合はtargetに最も近いものが優先です。

別fileは`--config <path>`で指定できます。absolute pathはそのまま使います。relative pathは互換性のためcurrent working directoryを先に確認し、その後target directory基準も確認します。build systemがworking directoryを変更してもproject-relative設定を使えます。明示指定した設定が見つからない場合は、黙ってdefaultへfallbackせずerrorにします。

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

- `ignoredPaths` - 一致するfile/directoryを解析対象から除外します。
- `disabledRules` - 指定rule IDをそのrun/projectでは完全に抑制します。
- `failureThreshold` - processが失敗終了するdiagnostic levelを指定します。
- `ruleSettings.oop304.warningDepth` - OOP304を出すproject-owned class継承深度を指定します。既定は`4`、最小`1`です。同じ解析に読み込まれた別projectのbaseは数えますが、外部framework/libraryの祖先は数えません。

コピー可能な例は`oop-design-checker.example.json`を参照してください。

## GUI

```bash
dotnet run --project src/frontends/gui/OopDesignChecker.Gui.csproj
```

GUIではtarget pathと任意のconfiguration pathを選択し、CUIと同じ解析結果を表示します。native file/folder picker、structured diagnostic table、filter、detail pane、config editor、cancel、JSON/SARIF exportを利用できます。

## Build

projectは.NET 10をtargetとします。cross-platform CIでshared engine、CUI、GUIをWindows / Linux / macOS上でbuildします。

CUI publish例:

```bash
dotnet publish src/frontends/cui/OopDesignChecker.Cui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

必要に応じて`win-x64`を`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`へ置き換えます。配布時はRoslyn/MSBuild support fileを欠落させないため、publish directory全体を保持してください。

## ドキュメント

- [English README](README.en.md)
- [Changelog（日本語・正本）](CHANGELOG.md)
- [Changelog (English)](CHANGELOG.en.md)
- [オブジェクト指向設計の定義](specification/object-oriented-design.md)
- [チェックルール](specification/check-rules.md)
- [Object-oriented design definition (English)](specification/object-oriented-design.en.md)
- [Check rules (English)](specification/check-rules.en.md)
