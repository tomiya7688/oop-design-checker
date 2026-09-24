# oop-design-checker

[日本語（正本）](README.md)

> The Japanese documentation is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

Static-analysis tool for checking whether an object-oriented project follows the object-oriented design rules defined by this repository.

The checker itself is implemented in C# and is intended to pass its own rules without suppressing violations by default.

## Frontends

The same analysis engine is exposed through sibling frontends:

- `src/frontends/cui` - CI-friendly command-line frontend
- `src/frontends/gui` - cross-platform Avalonia GUI frontend

The legacy `src/OopDesignChecker` executable remains available for compatibility.

## Diagnostic levels

- `DANGER` - incompatible with the OOP rules this project treats as fundamental. This fails CI by default.
- `WARN` - normally undesirable in OOP design, but not inherently fatal.
- `ATTN` - stricter-design guidance where performance, flexibility, framework constraints, or other tradeoffs may justify the design.

The checker itself is tested with `--fail-on attention` so its own source must satisfy all three levels.

## Current implementation

The first implementation targets C# through Roslyn and MSBuild. Real project compilations are preserved when analyzing `.csproj`, `.sln`, `.slnx`, or directories containing multiple C# projects; unrelated projects are never merged into one artificial compilation.

Implemented rules are defined in `specification/check-rules.en.md` and include encapsulation, inheritance, polymorphism, visibility, static-state, operation complexity, object-integrity, dependency, and navigation checks.

## Quality gates

GitHub Actions exposes separate checks so failures are easy to identify:

- `normal-ci` - restore, build, and rule smoke tests on Windows, Linux, and macOS
- `self-check` - runs the checker against its own `src` tree with `--fail-on attention`
- `code-analyzers` - .NET SDK analyzers at `latest-recommended`, code style enabled, warnings treated as errors
- `csharpier` - CSharpier 1.3.0 formatting check using the repository-local tool manifest
- `release` - validates distributable CUI/GUI packages and the exact release fixture gate for Windows, Linux, and macOS when release-related files change

## CUI

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path>
```

`<target-path>` can be a loose `.cs` file, a `.csproj`, a `.sln`, a `.slnx`, or a directory. A directory containing multiple C# projects is analyzed as a project set: each project keeps its own MSBuild compilation and the resulting diagnostics are aggregated and deduplicated.

Show the product version:

```bash
oop-design-checker-cui --version
```

The short alias is `-V`.

Choose the CI failure threshold:

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on danger
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on warning
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on attention
```

`--warnings-as-errors` remains as a compatibility alias for `--fail-on warning`.

Choose the human-readable display language:

```bash
oop-design-checker-cui <target-path> --language ja
oop-design-checker-cui <target-path> --language en
```

`ja` is the default. CUI `text` output and the GUI localize human-facing rule titles, diagnostic messages, and summaries. Rule IDs such as `OOP001`, CLI option names, and JSON configuration keys stay stable. `json`, `sarif`, and `github` output keep canonical language-stable diagnostic text for downstream compatibility.

### Release packages

Pushing a matching version tag such as `v0.1.0` creates a GitHub Release after all platform packages build successfully. The product version is declared once in `Directory.Build.props`; a `v*` tag that does not match that declared version fails before release packaging is published.

Release archives contain the complete `dotnet publish` output, both README files, both CHANGELOG files, `oop-design-checker.example.json`, and the specification. CUI archives use `oop-design-checker-<version>-<rid>` and GUI archives use `oop-design-checker-gui-<version>-<rid>`. Every archive also has a `.sha256` file, and tagged releases include a combined `SHA256SUMS.txt` manifest.

Published targets are:

- `win-x64` and `win-arm64` as `.zip`
- `linux-x64` and `linux-arm64` as `.tar.gz`
- `osx-x64` and `osx-arm64` as `.tar.gz`

The CUI and GUI packages are self-contained, so the matching .NET runtime does not need to be installed just to start them. The GUI is released for the same six RIDs. Native runners perform startup smoke tests on the RIDs they can execute. ARM64 remains a release target, but it is not part of the user's manual hardware sign-off; automated build, packaging, and checksum validation remain required. Analysis of `.csproj`, `.sln`, and `.slnx` still depends on a discoverable compatible .NET SDK/MSBuild installation because those targets are loaded through MSBuild. Loose `.cs` analysis does not require project loading.

For example, after extracting the matching package:

```bash
./oop-design-checker-cui <target-path> --fail-on danger
```

On Windows use `oop-design-checker-cui.exe`.

### Release process

Before creating a release tag, run the `release-candidate` workflow once. That workflow run is the normative release candidate.

1. Finalize version / changelog metadata, licensing / third-party notices, and the normative specification.
2. Select the release commit and manually dispatch `release-candidate` from that commit/ref. The workflow-start commit SHA is fixed as the candidate SHA.
3. For that same SHA, pass normal CI, strict self-check, analyzers, CSharpier, exact release-fixture validation, six-RID CUI/GUI packaging, packaged E2E, Windows/macOS Appium, Headless visual regression, and the OpenAI/Gemini/Claude UI consensus gate.
4. A successful automatic gate stores `release-candidate-manifest` plus the candidate packages and evidence. The #105 manual GUI sign-off must use the GUI artifact and shared release fixture from that same run.
5. ARM64 remains an official package target but is excluded from the user's manual hardware sign-off; automated build, package, and checksum checks remain mandatory.
6. After #105 sign-off, verify that the candidate SHA has not changed and create the version-matching `v` tag on **that exact SHA**. Do not tag a different commit.
7. The tag-triggered `release` workflow revalidates the version, rebuilds packages/checksums, and creates the GitHub Release.

### CI output formats

The default `text` format stays concise and backward compatible. Machine-readable formats can be selected with `--format`:

```bash
# Stable JSON document
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format json --output oop-diagnostics.json

# SARIF 2.1.0 for code-scanning systems
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format sarif --output oop-diagnostics.sarif

# GitHub Actions workflow annotations
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- src --format github
```

Supported formats:

- `text` - normal `ATTN` / `WARN` / `DANGER` lines plus summary
- `json` - versioned diagnostic array and severity summary
- `sarif` - SARIF 2.1.0 with rule metadata, severity level, source location, and symbol metadata
- `github` - emits `::notice`, `::warning`, and `::error` workflow commands so findings appear as GitHub Actions annotations

`--output <file>` can be used with `text`, `json`, and `sarif`. GitHub annotations intentionally always go to stdout. Output format never changes the process exit code: `--fail-on` continues to control CI failure independently.

## Configuration

By default the checker searches for the nearest `oop-design-checker.json`, starting in the target directory and walking upward through parent directories. This allows a repository- or solution-root configuration to apply when MSBuild or CI invokes the checker against a nested `.csproj` without copying the configuration into each project or build-output directory. If multiple files exist, the nearest one to the target wins.

A different file can be selected with `--config <path>`. Absolute paths are used directly. Relative paths preserve the existing current-working-directory lookup first, then fall back to the target directory so build systems that change their working directory can still use project-relative configuration paths. An explicitly requested configuration that cannot be found is an error rather than a silent fallback to defaults.

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

- `ignoredPaths` excludes matching files/directories from analysis.
- `disabledRules` completely suppresses selected rule IDs for that run/project.
- `failureThreshold` controls which diagnostic level makes the process return a failing exit code.
- `ruleSettings.oop304.warningDepth` controls the project-owned class inheritance depth that triggers OOP304. The default is `4`; values must be at least `1`. Bases that belong to other projects loaded in the same analysis are counted, while external framework/library ancestry is not.

See `oop-design-checker.example.json` for a copyable example.

## GUI

```bash
dotnet run --project src/frontends/gui/OopDesignChecker.Gui.csproj
```

The GUI accepts a target path and an optional configuration path, then displays the same diagnostics produced by the CUI.

## Build

The project targets .NET 10. Cross-platform CI builds the shared engine, CUI, and GUI on Windows, Linux, and macOS.

Example CUI publish:

```bash
dotnet publish src/frontends/cui/OopDesignChecker.Cui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Replace `win-x64` with `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64` as needed. Keep the complete publish directory when distributing the checker so Roslyn/MSBuild support files are not accidentally omitted.

## Documentation

- [Japanese README (normative)](README.md)
- [Japanese changelog (normative)](CHANGELOG.md)
- [English changelog](CHANGELOG.en.md)
- [Design definition](specification/object-oriented-design.en.md)
- [Check rules](specification/check-rules.en.md)
