using OopDesignChecker.Output;

namespace OopDesignChecker.Gui;

internal static class GuiAutomationEnvironment
{
    private const string ExportDirectoryVariable = "OOP_DESIGN_CHECKER_UI_E2E_EXPORT_DIR";

    public static string? ExportPath(DiagnosticExportFormat format)
    {
        var directory = Environment.GetEnvironmentVariable(ExportDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        var extension = format == DiagnosticExportFormat.Sarif ? "sarif" : "json";
        return Path.Combine(directory, $"oop-design-checker-results.{extension}");
    }
}
