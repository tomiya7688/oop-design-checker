using OopDesignChecker.Application;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

internal static class DiagnosticWriterFactory
{
    public static IDiagnosticWriter Create(
        DiagnosticOutputFormat format,
        string? outputPath,
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese
    ) =>
        format switch
        {
            DiagnosticOutputFormat.Text => new ConsoleDiagnosticWriter(outputPath, language),
            DiagnosticOutputFormat.Json => new JsonDiagnosticWriter(outputPath),
            DiagnosticOutputFormat.Sarif => new SarifDiagnosticWriter(outputPath),
            DiagnosticOutputFormat.GitHub => new GitHubAnnotationDiagnosticWriter(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
