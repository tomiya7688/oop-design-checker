using System.Reflection;
using Avalonia;

namespace OopDesignChecker.Gui;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--version" or "-V")
        {
            Console.WriteLine(GetProductVersion());
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
