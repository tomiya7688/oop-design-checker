# Japanese Identifiers and Comments in C# Source

## Background and purpose

The checker's analysis and application are primarily implemented in C#. C# identifiers can contain Unicode characters, so implementation-owned internal type, member, and variable names can use Japanese where that makes their meaning easier to understand.

The purpose is to make intent easier to read from the source. Keep a name unchanged when Japanese would make its meaning less clear or when it is part of an external contract.

## Names to write in Japanese

- Use Japanese for `private` and `internal` types, members, local variables, and parameters when it makes their meaning clear.
- Write implementation-owned source comments in Japanese and follow [JSON-like Comment Outs](https://github.com/tomiya7688/Json-like-comment-outs).
- Use nouns that describe the subject or role for types, verbs that describe the operation for methods, and nouns that describe the value for variables.
- Do not romanize Japanese words as identifier names.
- Keep standard technical terms in English when a Japanese replacement would be less clear.

## Names that remain in English

Keep these names because they are external contracts or established points of use:

- `public` CLR types and members, and shared contracts between assemblies
- namespace, assembly, package, source file, and directory names
- CLI options, configuration keys, JSON/SARIF/GitHub annotation fields, and OOP rule IDs
- UI automation selectors and accessibility names
- C# keywords and framework/library APIs such as .NET, Roslyn, and Avalonia

Japanese identifiers use Unicode Normalization Form C (NFC). Do not change generated names or names defined by external specifications.

## How to verify

Run these commands from `tools/oop-design-checker` to verify the build, tests, and formatting.

```powershell
dotnet build OopDesignChecker.sln -c Release -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true
dotnet run --project tests/OopDesignChecker.Tests/OopDesignChecker.Tests.csproj -c Release
dotnet csharpier check .
```

Also verify Windows, Linux, and macOS in GitHub Actions. Identifier changes must not alter diagnostics, CLI output, configuration values, JSON/SARIF formats, or UI automation contracts.
