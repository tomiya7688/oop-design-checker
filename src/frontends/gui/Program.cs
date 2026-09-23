using System.Reflection;
using Avalonia;

namespace OopDesignChecker.Gui;

internal static class Program
{
    private const string AutomationExportDirectoryOption = "--ui-automation-export-dir";

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--version" or "-V")
        {
            Console.WriteLine(GetProductVersion());
            return;
        }

        var desktopArguments = ApplyAutomationArguments(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(desktopArguments);
    }

    private static string[] ApplyAutomationArguments(string[] args)
    {
        var desktopArguments = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] != AutomationExportDirectoryOption)
            {
                desktopArguments.Add(args[index]);
                continue;
            }

            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new InvalidOperationException(
                    $"{AutomationExportDirectoryOption} requires a directory path."
                );
            }

            Environment.SetEnvironmentVariable(
                "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR",
                args[++index]
            );
        }

        return [.. desktopArguments];
    }

    private static string GetProductVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "unknown";
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
