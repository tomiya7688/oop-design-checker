# 変更履歴

> **この日本語版が正本です。** 英語互換版は [CHANGELOG.en.md](CHANGELOG.en.md) を参照してください。内容に差異がある場合は、この日本語版を優先します。

## 0.1.0 - 2026-09-16

`oop-design-checker` の初回公開リリース。

### 追加

- Roslyn / MSBuild を利用した C# オブジェクト指向設計解析。
- Danger / Warning / Attention の明示的severityモデル。
- カプセル化、継承、ポリモーフィズム、可視性、static state、operation complexity、object integrity、coupling、object navigationの検査。
- `.cs`、`.csproj`、`.sln`、`.slnx`、複数project directoryの解析。
- 共有解析coreとCUI / Avalonia GUI frontend。
- JSON、SARIF 2.1.0、GitHub Actions annotation、簡潔なtext出力。
- ignored path、disabled rule、CI failure thresholdの設定。
- nested project/build invocation向けの親directory設定探索。
- Windows / Linux / macOS の x64 / arm64 向け self-contained CUI release package。
- strict self-analysis、.NET Analyzer、CSharpier、3 OS CI quality gate。

### 修正

- CUI parserが相対 `--config` をprocess current working directoryへ固定せず、target-relative fallbackを維持するよう修正。
