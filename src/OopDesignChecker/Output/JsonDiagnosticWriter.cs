using System.Text.Json;
using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

internal sealed class JsonDiagnosticWriter : IDiagnosticWriter
{
    private readonly DiagnosticOutputTarget _target;

    public JsonDiagnosticWriter(string? outputPath = null)
    {
        _target = new DiagnosticOutputTarget(outputPath);
    }

    public void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose)
    {
        var document = new JsonDiagnosticDocument(
            1,
            diagnostics.Select(ToItem).ToArray(),
            new JsonDiagnosticSummary(
                diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Danger),
                diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Warning),
                diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Attention)
            )
        );

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        _target.Write(writer => writer.Write(JsonSerializer.Serialize(document, serializerOptions)));
    }

    private static JsonDiagnosticItem ToItem(DesignDiagnostic diagnostic) =>
        new(
            diagnostic.Rule.Id,
            diagnostic.Rule.Title,
            diagnostic.Severity.ToString().ToLowerInvariant(),
            diagnostic.Message,
            diagnostic.SymbolName,
            diagnostic.Location.FilePath,
            diagnostic.Location.Line,
            diagnostic.Location.Column
        );

    private sealed record JsonDiagnosticDocument(
        int Version,
        IReadOnlyList<JsonDiagnosticItem> Diagnostics,
        JsonDiagnosticSummary Summary
    );

    private sealed record JsonDiagnosticItem(
        string RuleId,
        string Title,
        string Severity,
        string Message,
        string? Symbol,
        string File,
        int Line,
        int Column
    );

    private sealed record JsonDiagnosticSummary(int Danger, int Warning, int Attention);
}
