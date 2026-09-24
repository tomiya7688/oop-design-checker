# Changelog

[日本語（正本）](CHANGELOG.md)

> The Japanese changelog is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

## 1.0.0 - 2026-09-24

Stable 1.0.0 release of the C# implementation.

### Added

- Both CUI and Avalonia GUI as official release artifacts.
- Self-contained CUI/GUI packages for six RIDs across Windows, Linux, and macOS on x64 and arm64.
- Release-fixture coverage for positive, boundary, and negative cases across every current OOPxxx rule.
- Exact release gating against 66 canonical diagnostics.
- Release validation for JSON/text/SARIF semantic consistency, target entrypoints, configuration, and failure thresholds.
- Windows/macOS Appium real-window E2E plus Avalonia.Headless + Skia visual regression.
- Independent OpenAI, Gemini, and Claude UI judges with a consensus gate.
- Unified release-candidate workflow with a fixed candidate commit SHA.
- GUI configuration editor, filtering, detail view, copy, source open, JSON/SARIF export, cancellation, and Japanese/English switching.

### Improved

- Built-in defaults when no config is supplied, with nearest parent-directory config discovery.
- More robust directory, multi-project, .csproj, .sln, and .slnx analysis.
- Roslyn MSBuild BuildHost and .NET reference assemblies included in packaged CUI/GUI artifacts.
- Stronger handling for diagnostic paths, case-sensitive path identity, invalid enum values, and data-carrier boundaries.
- Published-artifact E2E for exit codes 0/1/2, extracted archives, and checksums.

### Compatibility and limitations

- The 1.0.0 analysis backend supports C# only. C++, Go, and Python are planned for 1.1.0.
- .csproj, .sln, and .slnx analysis requires a compatible discoverable .NET SDK/MSBuild installation.
- ARM64 packages are official release targets but are outside the user's manual hardware sign-off; automated build, packaging, and checksum validation remain mandatory.

## 0.1.0 - 2026-09-16

Initial public release of `oop-design-checker`.

### Added

- C# object-oriented design analysis based on Roslyn and MSBuild.
- Explicit rule severities: Danger, Warning, and Attention.
- Encapsulation, inheritance, polymorphism, visibility, static-state, operation-complexity, object-integrity, coupling, and object-navigation checks.
- `.cs`, `.csproj`, `.sln`, `.slnx`, and multi-project directory analysis.
- Shared analysis core with CUI and Avalonia GUI frontends.
- JSON, SARIF 2.1.0, GitHub Actions annotation, and concise text output.
- Configurable ignored paths, disabled rules, and CI failure thresholds.
- Parent-directory configuration discovery for nested project/build invocation.
- Windows, Linux, and macOS self-contained CUI release packages for x64 and arm64.
- Strict self-analysis, .NET analyzers, CSharpier, and three-OS CI quality gates.

### Fixed

- Relative `--config` paths now preserve target-relative fallback instead of being forced to the process working directory by the CUI parser.
