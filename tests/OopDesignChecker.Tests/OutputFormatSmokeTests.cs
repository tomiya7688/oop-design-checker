using System.Text.Json;
using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class OutputFormatSmokeTests
{
    public static void Run()
    {
        CommandLineParsesFormatAndOutput();
        GitHubFormatRejectsOutputFile();
        JsonOutputIsMachineReadable();
        SarifOutputIsMachineReadable();
        GitHubAnnotationsEscapeSpecialCharacters();
    }

    private static void CommandLineParsesFormatAndOutput()
    {
        var result = CommandLineOptionsParser.Parse([
            "sample.cs",
            "--format",
            "sarif",
            "--output",
            "results.sarif",
        ]);

        if (
            !result.IsSuccess
            || result.Options is null
            || result.Options.OutputFormat != DiagnosticOutputFormat.Sarif
            || !result.Options.OutputPath!.EndsWith("results.sarif", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Expected SARIF output CLI options to parse.");
        }
    }

    private static void GitHubFormatRejectsOutputFile()
    {
        var result = CommandLineOptionsParser.Parse([
            "sample.cs",
            "--format",
            "github",
            "--output",
            "annotations.txt",
        ]);

        if (result.IsSuccess)
        {
            throw new InvalidOperationException(
                "GitHub annotation output should reject --output because annotations must use stdout."
            );
        }
    }

    private static void JsonOutputIsMachineReadable()
    {
        var path = CreateTemporaryPath("json");
        try
        {
            new JsonDiagnosticWriter(path).Write([CreateDiagnostic()], verbose: true);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            if (
                root.GetProperty("version").GetInt32() != 1
                || root.GetProperty("diagnostics")[0].GetProperty("ruleId").GetString() != "OOP999"
                || root.GetProperty("summary").GetProperty("warning").GetInt32() != 1
            )
            {
                throw new InvalidOperationException(
                    "JSON diagnostic document had unexpected content."
                );
            }
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static void SarifOutputIsMachineReadable()
    {
        var path = CreateTemporaryPath("sarif");
        try
        {
            new SarifDiagnosticWriter(path).Write([CreateDiagnostic()], verbose: false);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var result = root.GetProperty("runs")[0].GetProperty("results")[0];

            if (
                root.GetProperty("version").GetString() != "2.1.0"
                || result.GetProperty("ruleId").GetString() != "OOP999"
                || result.GetProperty("level").GetString() != "warning"
            )
            {
                throw new InvalidOperationException("SARIF document had unexpected content.");
            }
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static void GitHubAnnotationsEscapeSpecialCharacters()
    {
        var originalOut = Console.Out;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            var diagnostic = CreateDiagnostic(
                filePath: Path.Combine("sample,dir", "file:part.cs"),
                message: "first%line\nsecond"
            );
            new GitHubAnnotationDiagnosticWriter().Write([diagnostic], verbose: false);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = captured.ToString();
        if (
            !output.Contains("::warning ", StringComparison.Ordinal)
            || !output.Contains("%2C", StringComparison.Ordinal)
            || !output.Contains("%3A", StringComparison.Ordinal)
            || !output.Contains("%25", StringComparison.Ordinal)
            || !output.Contains("%0A", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"GitHub annotation escaping was incomplete: {output}"
            );
        }
    }

    private static DesignDiagnostic CreateDiagnostic(
        string? filePath = null,
        string message = "Sample diagnostic"
    ) =>
        new(
            new RuleDescriptor("OOP999", "Sample rule", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            message,
            "Sample.Member",
            new SourceLocation(filePath ?? Path.GetFullPath("sample.cs"), 3, 7)
        );

    private static string CreateTemporaryPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"oop-checker-{Guid.NewGuid():N}.{extension}");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
