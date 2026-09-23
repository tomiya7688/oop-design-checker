using OopDesignChecker.Output;

namespace OopDesignChecker.Gui;

internal static class UiAutomationSettings
{
    private const string ExportDirectoryVariable = "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR";
    private const string AnalysisDelayVariable =
        "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";

    internal static int AnalysisDelayMilliseconds
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(AnalysisDelayVariable);
            return int.TryParse(value, out var parsed) && parsed is >= 0 and <= 30000 ? parsed : 0;
        }
    }

    internal static string? ExportPath(DiagnosticExportFormat format)
    {
        var directory = Environment.GetEnvironmentVariable(ExportDirectoryVariable);
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
