using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

internal sealed class GitHubAnnotationDiagnosticWriter : IDiagnosticWriter
{
    public void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose)
    {
        foreach (var diagnostic in diagnostics)
        {
            var annotation = FormatAnnotationLevel(diagnostic.Severity);
            var file = EscapeProperty(diagnostic.Location.FilePath);
            var title = EscapeProperty($"{diagnostic.Rule.Id} {diagnostic.Rule.Title}");
            var message = EscapeMessage(
                diagnostic.SymbolName is null
                    ? diagnostic.Message
                    : $"{diagnostic.SymbolName}: {diagnostic.Message}"
            );

            Console.WriteLine(
                $"::{annotation} file={file},line={diagnostic.Location.Line},col={diagnostic.Location.Column},title={title}::{message}"
            );
        }
    }

    private static string FormatAnnotationLevel(DesignDiagnosticSeverity severity) =>
        severity switch
        {
            DesignDiagnosticSeverity.Danger => "error",
            DesignDiagnosticSeverity.Warning => "warning",
            DesignDiagnosticSeverity.Attention => "notice",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    private static string EscapeProperty(string value) =>
        EscapeMessage(value).Replace(":", "%3A", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal);

    private static string EscapeMessage(string value) =>
        value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
}
