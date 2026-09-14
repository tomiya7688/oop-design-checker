using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

internal sealed class ConsoleDiagnosticWriter : IDiagnosticWriter
{
    public void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose)
    {
        foreach (var diagnostic in diagnostics)
        {
            var symbol = diagnostic.SymbolName is null ? string.Empty : $" {diagnostic.SymbolName}";
            Console.WriteLine(
                $"{FormatSeverity(diagnostic.Severity)} {diagnostic.Rule.Id} "
                + $"{diagnostic.Location.FilePath}:{diagnostic.Location.Line}:{diagnostic.Location.Column}{symbol}: "
                + diagnostic.Message);

            if (verbose)
            {
                Console.WriteLine($"  {diagnostic.Rule.Title}");
            }
        }

        var dangerCount = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Danger);
        var warningCount = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Warning);
        var attentionCount = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Attention);

        Console.WriteLine(
            $"Diagnostics: {dangerCount} danger, {warningCount} warning(s), {attentionCount} attention.");
    }

    private static string FormatSeverity(DesignDiagnosticSeverity severity) => severity switch
    {
        DesignDiagnosticSeverity.Attention => "ATTN",
        DesignDiagnosticSeverity.Warning => "WARN",
        DesignDiagnosticSeverity.Danger => "DANGER",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };
}
