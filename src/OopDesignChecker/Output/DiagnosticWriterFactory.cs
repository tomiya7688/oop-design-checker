using OopDesignChecker.Application;

namespace OopDesignChecker.Output;

internal static class DiagnosticWriterFactory
{
    public static IDiagnosticWriter Create(DiagnosticOutputFormat format, string? outputPath) =>
        format switch
        {
            DiagnosticOutputFormat.Text => new ConsoleDiagnosticWriter(outputPath),
            DiagnosticOutputFormat.Json => new JsonDiagnosticWriter(outputPath),
            DiagnosticOutputFormat.Sarif => new SarifDiagnosticWriter(outputPath),
            DiagnosticOutputFormat.GitHub => new GitHubAnnotationDiagnosticWriter(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
