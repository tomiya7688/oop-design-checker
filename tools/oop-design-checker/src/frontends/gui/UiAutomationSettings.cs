using OopDesignChecker.Output;

namespace OopDesignChecker.Gui;

internal static class UI自動化設定
{
    private const string 出力先環境変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR";
    private const string 解析遅延環境変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";

    internal static int 解析遅延ミリ秒
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(解析遅延環境変数名);
            return int.TryParse(value, out var parsed) && parsed is >= 0 and <= 30000 ? parsed : 0;
        }
    }

    internal static string? 出力先パス(DiagnosticExportFormat format)
    {
        var directory = Environment.GetEnvironmentVariable(出力先環境変数名);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var fileName =
            format == DiagnosticExportFormat.Sarif
                ? "actual-diagnostics.sarif"
                : "actual-diagnostics.json";
        return Path.Combine(directory, fileName);
    }
}
