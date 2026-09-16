using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

public enum DiagnosticExportFormat
{
    Json,
    Sarif,
}

public static class DiagnosticExportService
{
    public static void Export(
        IReadOnlyList<DesignDiagnostic> diagnostics,
        string outputPath,
        DiagnosticExportFormat format
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        IDiagnosticWriter writer = format switch
        {
            DiagnosticExportFormat.Json => new JsonDiagnosticWriter(outputPath),
            DiagnosticExportFormat.Sarif => new SarifDiagnosticWriter(outputPath),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
        writer.Write(diagnostics, verbose: false);
    }
}
