using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

internal sealed class ConsoleDiagnosticWriter : IDiagnosticWriter
{
    private readonly DiagnosticOutputTarget _target;
    private readonly UserInterfaceLanguage _language;

    public ConsoleDiagnosticWriter(
        string? outputPath = null,
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese
    )
    {
        _target = new DiagnosticOutputTarget(outputPath);
        _language = language;
    }

    public void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose) =>
        _target.Write(writer => WriteDiagnostics(writer, diagnostics, verbose));

    private void WriteDiagnostics(
        TextWriter writer,
        IReadOnlyList<DesignDiagnostic> diagnostics,
        bool verbose
    )
    {
        foreach (var diagnostic in diagnostics)
        {
            var symbol = diagnostic.SymbolName is null ? string.Empty : $" {diagnostic.SymbolName}";
            writer.WriteLine(
                $"{FormatSeverity(diagnostic.Severity)} {diagnostic.Rule.Id} "
                    + $"{diagnostic.Location.FilePath}:{diagnostic.Location.Line}:{diagnostic.Location.Column}{symbol}: "
                    + diagnostic.Message
            );

            if (verbose)
            {
                writer.WriteLine($"  {diagnostic.Rule.Title}");
            }
        }

        var dangerCount = diagnostics.Count(item =>
            item.Severity == DesignDiagnosticSeverity.Danger
        );
        var warningCount = diagnostics.Count(item =>
            item.Severity == DesignDiagnosticSeverity.Warning
        );
        var attentionCount = diagnostics.Count(item =>
            item.Severity == DesignDiagnosticSeverity.Attention
        );

        writer.WriteLine(
            LocalizedText.DiagnosticsSummary(_language, dangerCount, warningCount, attentionCount)
        );
    }

    private static string FormatSeverity(DesignDiagnosticSeverity severity) =>
        severity switch
        {
            DesignDiagnosticSeverity.Attention => "ATTN",
            DesignDiagnosticSeverity.Warning => "WARN",
            DesignDiagnosticSeverity.Danger => "DANGER",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
}
