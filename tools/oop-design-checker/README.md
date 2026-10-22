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

規則の見出しも確認する場合は、`--format text --verbose`を指定してください。`--verbose`はtext本文だけに見出しを追加し、JSON・SARIF・GitHub annotationの本文は変更しません。設定ファイルを読み込んだ場合は、そのパスも通知します。通知先はtextの標準出力時にはstdout、それ以外ではstderrです。内部の共通writer契約は診断一覧だけを受け取り、言語と詳細表示はtext writerの生成時に設定します。

値が必要なオプションの直後に別の既知オプションがある場合は、値の省略としてエラーにします。例えば`--output --verbose`では、`--verbose`を出力先にしません。`-report.json`のようなファイル名は値として使えます。オプションと同名のファイルは、`--output ./--verbose`のように相対パスで指定してください。

### 出力契約の責務

CLIとGUIの保存処理を独立して保守できるよう、共有writerの形式と生成処理は`Output`が所有します。`Output`は`Application`の型を参照しません。

| 契約 | 対応形式と責務 |
|---|---|
| `Output.診断出力形式` | text・JSON・SARIF・GitHub annotationを表す内部の共有契約です。 |
| `Application`のCLI引数解析 | `--format`を共有形式へ変換します。未定義値・数値形式を拒否し、GitHub annotationでの`--output`を禁止します。 |
| `Output.DiagnosticExportFormat` | JSON・SARIFだけを公開する保存専用の契約です。公開型名と数値は維持します。 |
| `DiagnosticExportService.Export` | 診断一覧と保存先を検証し、`ファイル保存用を作る`から取得したwriterで保存します。未定義形式は書き込み前に拒否します。 |

CLIでは上記の`--format`を指定してください。GUIからはJSONまたはSARIFを選んで保存してください。新しい共有writerを追加するときは、CLIで選択できる形式とGUIで保存できる形式をそれぞれ確認してください。

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

### ライブラリAPIから設定を作る場合

JSON以外から構築した設定も安全に解析へ渡せるよう、`CheckerConfiguration`と配下の設定型が不変条件を所有します。従来と同じ公開型・initプロパティを使用してください。

```csharp
var 無視パス = new List<string> { "**/Generated/**" };
var 設定 = new CheckerConfiguration { IgnoredPaths = 無視パス };
無視パス.Clear(); // 設定.IgnoredPathsには元のパスが残ります。
```

- 未定義の`FailureThreshold`、nullのリスト・`RuleSettings`・`Oop304`、1未満の`WarningDepth`は、initで指定した時点で`InvalidOperationException`になります。後から`Validate`や解析を呼ぶまで、不正値を保持することはできません。例外の説明には従来の設定キーを使います。
- `IgnoredPaths`と`DisabledRules`は、代入時に読み取り専用コピーを作ります。入力元の配列・Listを変更しても、設定は変わりません。公開一覧を`IList<string>`へキャストしても書き換えられません。コピー中に入力を別スレッドから変更しないでください。コピーには要素数に比例する時間とメモリを使います。
- `CheckerConfigurationJson.Parse`は、JSON入力用の型へ読み込んだ後で設定モデルを生成します。重複キーは従来どおり最後の値を使い、最終値の検証とリストのコピーをモデルへ委譲します。JSONキー・既定値・形式の受理条件・エラー説明は維持します。
- 公開`CheckerConfigurationJson.Validate`は互換性のため維持します。非nullの設定は生成時に検証済みなので再検証を必要とせず、null引数だけを拒否します。

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
- nullの診断一覧・結果へ渡す設定は、`ArgumentNullException`で拒否します。設定リストなどの不正値は、上記の設定モデルが生成時に拒否します。CLI引数、設定JSONのキー、診断出力形式は維持します。

### 解析処理の構成（開発者向け）

依存の生成方法を変更するたびに解析の流れまで修正することを避けるため、`CheckerService.Analyze`は既定構成で作った`解析ユースケース`へ処理を委譲します。公開メソッドの引数・戻り値と、CLI・GUIからの使い方は維持します。

| 役割 | 所有者 |
| --- | --- |
| 公開APIからの委譲 | `CheckerService` |
| 具体的な規則・ローダー・エンジンの組み立て | `既定の解析構成` |
| 対象の存在確認・絶対パス化・設定読込 | `解析入力読み込み器` |
| 呼び出し順、無効規則の除外、実効失敗基準、結果の組み立て | `解析ユースケース` |
| プロジェクトの読込と診断の生成 | 既存のローダーと`解析実行器` |

内部の差し替え口は、ユースケースのコンストラクターへ渡す4つの関数です。入力読込、規則供給、ローダー生成、診断生成をそれぞれ注入します。既定構成では従来の実装を接続し、ユースケーステストではメモリ上の依存を接続します。入力読込・規則・ローダー・解析で発生した例外は、そのまま呼び出し元へ伝わります。

構成を変更した場合は、モジュールのルートで次のテストを実行してください。17件のユースケーステストがファイルシステムとMSBuildを使わずに受け渡しと中断を確認し、2件の公開APIテストが対象確認と事前キャンセルの優先順を確認します。

```bash
dotnet run --project tests/OopDesignChecker.ContractTests -c Release -- --filter-class '*解析ユースケース契約テスト' '*公開解析API互換性テスト'
```

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
