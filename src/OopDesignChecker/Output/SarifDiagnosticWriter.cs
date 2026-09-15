using System.Text.Json;
using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

internal sealed class SarifDiagnosticWriter : IDiagnosticWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly DiagnosticOutputTarget _target;

    public SarifDiagnosticWriter(string? outputPath = null)
    {
        _target = new DiagnosticOutputTarget(outputPath);
    }

    public void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose)
    {
        var rules = diagnostics
            .GroupBy(diagnostic => diagnostic.Rule.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Rule.Id, StringComparer.Ordinal)
            .Select(ToRule)
            .ToArray();

        var run = new
        {
            tool = new
            {
                driver = new
                {
                    name = "oop-design-checker",
                    rules,
                },
            },
            results = diagnostics.Select(ToResult).ToArray(),
        };

        var document = new Dictionary<string, object?>
        {
            ["version"] = "2.1.0",
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["runs"] = new[] { run },
        };

        _target.Write(writer => writer.Write(JsonSerializer.Serialize(document, SerializerOptions)));
    }

    private static object ToRule(DesignDiagnostic diagnostic) =>
        new
        {
            id = diagnostic.Rule.Id,
            name = diagnostic.Rule.Title,
            shortDescription = new { text = diagnostic.Rule.Title },
            defaultConfiguration = new { level = FormatLevel(diagnostic.Severity) },
        };

    private static object ToResult(DesignDiagnostic diagnostic) =>
        new
        {
            ruleId = diagnostic.Rule.Id,
            level = FormatLevel(diagnostic.Severity),
            message = new { text = diagnostic.Message },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new
                        {
                            uri = new Uri(Path.GetFullPath(diagnostic.Location.FilePath)).AbsoluteUri,
                        },
                        region = new
                        {
                            startLine = diagnostic.Location.Line,
                            startColumn = diagnostic.Location.Column,
                        },
                    },
                },
            },
            properties = diagnostic.SymbolName is null
                ? null
                : new Dictionary<string, string> { ["symbol"] = diagnostic.SymbolName },
        };

    private static string FormatLevel(DesignDiagnosticSeverity severity) =>
        severity switch
        {
            DesignDiagnosticSeverity.Danger => "error",
            DesignDiagnosticSeverity.Warning => "warning",
            DesignDiagnosticSeverity.Attention => "note",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
}
