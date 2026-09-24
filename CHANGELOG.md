# Changelog

[English](CHANGELOG.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

## 1.0.0 - 2026-09-24

C#対応版を安定releaseとして確定するための1.0.0です。

### 追加

- CUI / Avalonia GUIの両frontendを正式なrelease artifactとして提供。
- Windows / Linux / macOSのx64 / arm64、計6 RID向けself-contained CUI/GUI package。
- release fixtureによる全OOPxxx ruleのpositive / boundary / negative検証。
- 66件のcanonical diagnosticを用いたexact release gate。
- JSON / text / SARIF間の意味整合、entrypoint差、config、failure thresholdのrelease検証。
- Windows/macOSのAppium実ウィンドウE2E、Avalonia.Headless + Skia visual regression。
- OpenAI / Gemini / Claudeの3社独立UI judgeと合議gate。
- release-candidate workflowによるcandidate SHA固定とautomatic gate統合。
- GUIのconfig editor、filter、detail、copy、source open、JSON/SARIF export、cancel、日英切替。

### 改善

- config未指定時のbuilt-in default利用と親directoryからの自動探索。
- directory / multi-project / .csproj / .sln / .slnx解析の安定化。
- packaged CUI/GUIへRoslyn MSBuild BuildHostと.NET reference assembliesを同梱。
- diagnostic path、case-sensitive path identity、invalid enum、data carrier等の境界処理を強化。
- published artifact E2Eでexit code 0 / 1 / 2、archive展開後実行、checksumを検証。

### 互換性・制限

- 1.0.0の解析backendはC#のみです。C++ / Go / Pythonは1.1.0で追加予定です。
- .csproj / .sln / .slnx解析には互換性のある.NET SDK/MSBuildが必要です。
- ARM64 packageは正式配布対象ですが、ユーザー本人によるmanual実機sign-off対象外です。build / package / checksumの自動検証をrelease gateとします。

## 0.1.0 - 2026-09-16

`oop-design-checker` の初回公開releaseです。

### 追加

- Roslyn / MSBuildを用いたC#オブジェクト指向設計解析。
- Danger / Warning / Attentionの明示的な診断レベル。
- カプセル化、継承、多態性、可視性、static state、operation complexity、object integrity、coupling、object navigationのチェック。
- `.cs`、`.csproj`、`.sln`、`.slnx`、複数project directoryの解析。
- CUI / Avalonia GUIで共有する解析core。
- JSON、SARIF 2.1.0、GitHub Actions annotation、簡潔なtext出力。
- ignored path、disabled rule、CI failure thresholdの設定。
- nested project/build invocation向けの親directory設定探索。
- Windows / Linux / macOS向けx64 / arm64 self-contained CUI release package。
- strict self-analysis、.NET analyzer、CSharpier、3 OS CI quality gate。

### 修正

- 相対`--config` pathをCUI parserがprocess working directoryへ固定せず、target-relative fallbackを維持するよう修正。
