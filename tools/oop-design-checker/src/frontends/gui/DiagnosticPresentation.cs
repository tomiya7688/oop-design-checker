using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

public sealed record DiagnosticRow(
    DesignDiagnosticSeverity Severity,
    string RuleId,
    string RuleTitle,
    string FilePath,
    int Line,
    int Column,
    string Message,
    string? SymbolName,
    UserInterfaceLanguage Language
)
{
    public string SeverityText => LocalizedText.SeverityName(Language, Severity);

    public string FileName => Path.GetFileName(FilePath);

    public string LocationText => $"{FilePath}:{Line}:{Column}";

    public string CopyText =>
        $"{SeverityText} {RuleId} {RuleTitle} {LocationText} {Message}"
        + (string.IsNullOrWhiteSpace(SymbolName) ? string.Empty : $" [{SymbolName}]");

    public static DiagnosticRow From(
        DesignDiagnostic diagnostic,
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese
    ) =>
        new(
            diagnostic.Severity,
            diagnostic.Rule.Id,
            DiagnosticTextLocalizer.Title(diagnostic, language),
            diagnostic.Location.FilePath,
            diagnostic.Location.Line,
            diagnostic.Location.Column,
            DiagnosticTextLocalizer.Message(diagnostic, language),
            diagnostic.SymbolName,
            language
        );
}

public sealed record DiagnosticFilterOptions(
    bool IncludeDanger,
    bool IncludeWarning,
    bool IncludeAttention,
    string SearchText
);

public static class DiagnosticFilter
{
    public static IReadOnlyList<DiagnosticRow> Apply(
        IEnumerable<DiagnosticRow> diagnostics,
        DiagnosticFilterOptions options
    ) =>
        diagnostics
            .Where(row => IncludesSeverity(row.Severity, options))
            .Where(row => MatchesSearch(row, options.SearchText))
            .OrderByDescending(row => row.Severity)
            .ThenBy(row => row.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Line)
            .ThenBy(row => row.Column)
            .ThenBy(row => row.RuleId, StringComparer.Ordinal)
            .ToArray();

    private static bool IncludesSeverity(
        DesignDiagnosticSeverity severity,
        DiagnosticFilterOptions options
    ) =>
        severity switch
        {
            DesignDiagnosticSeverity.Danger => options.IncludeDanger,
            DesignDiagnosticSeverity.Warning => options.IncludeWarning,
            DesignDiagnosticSeverity.Attention => options.IncludeAttention,
            _ => false,
        };

    private static bool MatchesSearch(DiagnosticRow row, string searchText)
    {
        var term = searchText.Trim();
        if (term.Length == 0)
        {
            return true;
        }

        return row.RuleId.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.RuleTitle.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.FilePath.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (row.SymbolName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
