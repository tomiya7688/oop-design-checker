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

        var errorCount = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Error);
        var warningCount = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Warning);
        var infoCount = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Info);

        Console.WriteLine($"Diagnostics: {errorCount} error(s), {warningCount} warning(s), {infoCount} info.");
    }

    private static string FormatSeverity(DesignDiagnosticSeverity severity) => severity switch
    {
        DesignDiagnosticSeverity.Info => "INFO",
        DesignDiagnosticSeverity.Warning => "WARN",
        DesignDiagnosticSeverity.Error => "ERROR",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };
}
