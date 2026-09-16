using OopDesignChecker.Application;
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

        var writer = DiagnosticWriterFactory.Create(ToOutputFormat(format), outputPath);
        writer.Write(diagnostics, verbose: false);
    }

    private static DiagnosticOutputFormat ToOutputFormat(DiagnosticExportFormat format) =>
        format switch
        {
            DiagnosticExportFormat.Json => DiagnosticOutputFormat.Json,
            DiagnosticExportFormat.Sarif => DiagnosticOutputFormat.Sarif,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
