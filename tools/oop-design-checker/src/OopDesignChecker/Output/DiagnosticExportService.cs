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

        var 書き込み器 = 診断書き込み器生成器.Create(出力形式に変換する(format), outputPath);
        書き込み器.Write(diagnostics, verbose: false);
    }

    private static 診断出力形式 出力形式に変換する(DiagnosticExportFormat format) =>
        format switch
        {
            DiagnosticExportFormat.Json => 診断出力形式.Json,
            DiagnosticExportFormat.Sarif => 診断出力形式.Sarif,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
