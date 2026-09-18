# Changelog

[English](CHANGELOG.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

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
