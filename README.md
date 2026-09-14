# oop-design-checker

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

The first implementation targets C# source through Roslyn.

Implemented rules:

- `OOP002` - polymorphism bypassed by repeated runtime type branching
- `OOP101` - public application class is visible more widely than its actual inheritance-hierarchy usage requires
- `OOP103` - instance method can be static
- `OOP104` - class with no meaningful instance state can be static
- `OOP106` - encapsulation leaks such as public mutable fields, directly exposed mutable collections, and unnecessarily public setters
- `OOP201` - oversized or overly complex operation
- `OOP303` - override disables inherited behavior by always rejecting it
- `OOP304` - excessive inheritance depth

More rules in `specification/check-rules.md` are design targets and will be implemented incrementally.

## CUI

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path>
```

Choose the CI failure threshold:

```bash
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on danger
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on warning
dotnet run --project src/frontends/cui/OopDesignChecker.Cui.csproj -- <target-path> --fail-on attention
```

`--warnings-as-errors` remains as a compatibility alias for `--fail-on warning`.

## Configuration

By default the checker looks for `oop-design-checker.json` in the target root. A different file can be selected with `--config <path>`.

```json
{
  "ignoredPaths": [
    "**/Generated/**",
    "**/*.g.cs"
  ],
  "disabledRules": [
    "OOP103"
  ],
  "failureThreshold": "danger"
}
```

- `ignoredPaths` excludes matching files/directories from analysis.
- `disabledRules` completely suppresses selected rule IDs for that run/project.
- `failureThreshold` controls which diagnostic level makes the process return a failing exit code.

See `oop-design-checker.example.json` for a copyable example.

## GUI

```bash
dotnet run --project src/frontends/gui/OopDesignChecker.Gui.csproj
```

The GUI accepts a target path and an optional configuration path, then displays the same diagnostics produced by the CUI.

## Build

The project targets .NET 10. Cross-platform CI builds the shared engine, CUI, and GUI and self-checks all source on Windows, Linux, and macOS.

Example single-file CUI publish:

```bash
dotnet publish src/frontends/cui/OopDesignChecker.Cui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Replace `win-x64` with another supported RID such as `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`.

## Specification

- [Design definition](specification/object-oriented-design.md)
- [Check rules](specification/check-rules.md)
