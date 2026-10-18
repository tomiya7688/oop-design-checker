# OOP Design Checker

[English](README.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

このtoolは、repository rootの[オブジェクト指向設計の定義](../../docs/object-oriented-design.md)を正本として、そのうち静的に観測できるsignalを補助確認するcheckerです。checkerは定義そのものではなく、検知できない原則も本文には存在します。

checker本体はC#で実装し、原則として自己違反を抑制せず、自分自身のruleを通過することを求めます。以下のcommand例は `tools/oop-design-checker/` をcurrent directoryとして記載します。

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

実装済みruleは[チェックルール仕様](docs/check-rules.md)で定義しています。カプセル化、継承、多態性、可視性、static state、operation complexity、object integrity、dependency、navigationなどを扱います。

## 品質ゲート

GitHub Actionsでは、失敗原因を切り分けやすいよう独立したcheckを用意しています。

- `normal-ci` - Windows / Linux / macOSでrestore、build、rule smoke test
- `self-check` - checker自身の`src`を`--fail-on attention`で解析
- `code-analyzers` - .NET SDK analyzerを`latest-recommended`で実行し、code style有効・warningをerror化
- `csharpier` - repository-local tool manifestのCSharpier 1.3.0でformat check
- `release` - release関連変更時にWindows / Linux / macOS向けCUI/GUI packageとrelease fixture exact gateを検証

開発時のC#日本語識別子・コメント方針は[こちら](docs/japanese-source-code.md)を参照してください。

## CUI

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path>
```

`<target-path>`には、単体`.cs`、`.csproj`、`.sln`、`.slnx`、directoryを指定できます。複数のC# projectを含むdirectoryでは、各projectのMSBuild compilationを独立して保持したままproject setとして解析し、診断だけを集約・重複除去します。

ソリューションがないフォルダーでは、直下に`.csproj`があっても配下のプロジェクトまで探索します。ソリューションがある場合は、そのソリューションに列挙されたプロジェクトを優先します。単体`.cs`を指定した場合は、周辺の`.csproj`を参照せず独立して解析します。

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

`v1.0.0`のように宣言versionと一致するtagをpushすると、全platform packageが成功した後にGitHub Releaseを作成します。製品versionは`Directory.Build.props`へ一元化しており、`v*` tagと宣言versionが一致しなければrelease packaging前に失敗します。

release archiveには`dotnet publish`の完全な出力、日英両README、日英両CHANGELOG、`oop-design-checker.example.json`、仕様書を含めます。CUIは`oop-design-checker-<version>-<rid>`、GUIは`oop-design-checker-gui-<version>-<rid>`として別archiveで配布します。各archiveには`.sha256`を付け、tagged releaseには統合`SHA256SUMS.txt`も含めます。

配布target:

- `win-x64` / `win-arm64`: `.zip`
- `linux-x64` / `linux-arm64`: `.tar.gz`
- `osx-x64` / `osx-arm64`: `.tar.gz`

CUI/GUI packageはself-containedなので、起動だけなら対応する.NET runtimeの事前installは不要です。GUIも同じ6 RIDを正式release対象とし、native runnerがあるRIDではarchive展開後まで起動smokeを行います。ARM64も配布対象ですが、ユーザー本人の実機manual確認は行わず、自動build/package/checksumをrelease gateとします。ただし`.csproj`、`.sln`、`.slnx`解析はMSBuild経由で読み込むため、互換性のある.NET SDK/MSBuildが検出可能である必要があります。単体`.cs`解析ではproject loadingは不要です。

展開後の例:

```bash
./oop-design-checker-cui <target-path> --fail-on danger
```

Windowsでは`oop-design-checker-cui.exe`を使用します。

### Release手順

正式releaseでは、tag作成前に`release-candidate` workflowを1回実行し、そのrunをrelease candidateの正本とします。

1. version / CHANGELOG、license / third-party notice、正式仕様を確定する。
2. release対象commitを選び、そのcommit/refから`release-candidate` workflowをmanual dispatchする。workflow開始時のcommit SHAがcandidate SHAとして固定される。
3. 同一candidate SHAに対してnormal CI、strict self-check、analyzer、CSharpier、release fixture exact check、CUI/GUI 6 RID package、packaged E2E、Windows/macOS Appium、Headless visual regression、OpenAI/Gemini/Claude UI合議を順に通す。
4. automatic gateがPASSすると`release-candidate-manifest`とcandidate artifact/evidenceが保存される。#105の手動GUI確認では、このrunのGUI artifactと共通release fixtureを使用する。
5. ARM64は正式package対象だが、ユーザー本人のmanual hardware sign-off対象外とし、自動build/package/checksumを必須とする。
6. #105 sign-off後、candidate SHAから変更がないことを確認し、**同じSHA**へ宣言versionと一致する`v` prefix付きtagを作成する。別SHAをtagしてはいけない。
7. tagによる`release` workflowがversion一致を再検証し、package/checksumを再生成してGitHub Releaseを作成する。

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

値が必要なオプションの直後に別の既知オプションがある場合は、値の省略としてエラーにします。例えば`--output --verbose`では、`--verbose`を出力先にしません。`-report.json`のようなファイル名は値として使えます。オプションと同名のファイルは、`--output ./--verbose`のように相対パスで指定してください。

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

## ライブラリAPIの解析結果

`CheckerService.Analyze(...)`が返す`CheckerRunResult`は、解析完了時点の診断と設定を保持します。入力リストを後から変更しても過去の結果が変わらないよう、診断一覧と設定の`IgnoredPaths` / `DisabledRules`をコピーし、読み取り専用の一覧として公開します。規則設定はinit専用プロパティだけで構成されるため共有します。

結果を利用する場合は、`Diagnostics`で診断を読み取り、`ShouldFail`で失敗判定を確認してください。

```csharp
var result = CheckerService.Analyze("src");
foreach (var diagnostic in result.Diagnostics)
    Console.WriteLine(diagnostic.Message);
var failed = result.ShouldFail;
```

- 公開コンストラクターの引数名、`IReadOnlyList<DesignDiagnostic>`型、分解代入、init、`with`を維持します。initと`with`で一覧や設定を差し替える場合もコピーします。入力と結果の一覧・設定の参照同一性には依存しないでください。
- `FailureThreshold`は上書き指定を反映した実効基準です。`Configuration.FailureThreshold`は元の設定値です。`ShouldFail`は現在の診断と実効基準から毎回計算するため、`with { FailureThreshold = ... }`でも整合します。空一覧では失敗しません。
- recordの比較では、一覧と設定は従来どおり参照で比較し、内容比較は導入しません。変更のない`with { }`は同じスナップショットを共有し、等価性とハッシュ値を維持します。同じ入力から個別に構築した結果や、一覧・設定を再代入した結果は、新しいコピーの参照を持つため等価とは限りません。
- コピー時には要素数に比例した時間とメモリを使います。`ShouldFail`の計算量は従来どおり最大で診断数に比例します。コピー中の入力を別スレッドから変更しないでください。
- nullの診断一覧・設定・設定内リストはコピー時に`ArgumentNullException`で拒否します。設定モデル全体の妥当性検証は、この結果型では行いません。CLI引数、設定JSONのキー、診断出力形式は維持します。

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
- [オブジェクト指向設計の定義](../../docs/object-oriented-design.md)
- [チェックルール](docs/check-rules.md)
- [Object-oriented design definition (English)](../../docs/object-oriented-design.en.md)
- [Check rules (English)](docs/check-rules.en.md)
