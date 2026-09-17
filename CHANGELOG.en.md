# Changelog

> **English compatibility edition.** The Japanese [CHANGELOG.md](CHANGELOG.md) is canonical. If this translation differs from the Japanese version, the Japanese version takes precedence.

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
