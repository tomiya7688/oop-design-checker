using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

internal sealed class GitHubAnnotationDiagnosticWriter : IDiagnosticWriter
{
    private readonly UserInterfaceLanguage _language;

    public GitHubAnnotationDiagnosticWriter(
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese
    )
    {
        _language = language;
    }

    public void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose)
    {
        foreach (var diagnostic in diagnostics)
        {
            var annotation = FormatAnnotationLevel(diagnostic.Severity);
            var file = EscapeProperty(diagnostic.Location.FilePath);
            var localizedTitle = UserInterfaceText.RuleTitle(
                diagnostic.Rule.Id,
                diagnostic.Rule.Title,
                _language
            );
            var title = EscapeProperty($"{diagnostic.Rule.Id} {localizedTitle}");
            var localizedMessage = UserInterfaceText.DiagnosticMessage(
                diagnostic.Rule.Id,
                diagnostic.Message,
                _language
            );
            var message = EscapeMessage(
                diagnostic.SymbolName is null
                    ? localizedMessage
                    : $"{diagnostic.SymbolName}: {localizedMessage}"
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
        EscapeMessage(value)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);

    private static string EscapeMessage(string value) =>
        value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
}
