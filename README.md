# oop-design-checker

Static-analysis tool for checking whether an object-oriented project follows the object-oriented design rules defined by this repository.

The checker itself is implemented in C# and is intended to pass its own rules without suppressing violations by default.

## Current implementation

The first implementation targets C# source through Roslyn.

Implemented rules:

- `OOP002` - polymorphism bypassed by repeated runtime type branching
- `OOP103` - instance method can be static
- `OOP104` - class with no meaningful instance state can be static
- `OOP106` - public field leaks object representation
- `OOP201` - oversized or overly complex operation
- `OOP303` - override disables inherited behavior by always rejecting it
- `OOP304` - excessive inheritance depth

More rules in `specification/check-rules.md` are design targets and will be implemented incrementally.

## Run

```bash
dotnet run --project src/OopDesignChecker/OopDesignChecker.csproj -- <target-path>
```

Treat warnings as failures:

```bash
dotnet run --project src/OopDesignChecker/OopDesignChecker.csproj -- <target-path> --warnings-as-errors
```

## Build

The project targets .NET 10. Cross-platform CI builds and self-checks the checker on Windows, Linux, and macOS.

Example single-file publish:

```bash
dotnet publish src/OopDesignChecker/OopDesignChecker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Replace `win-x64` with another supported RID such as `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64`.

## Specification

- [Design definition](specification/object-oriented-design.md)
- [Check rules](specification/check-rules.md)
