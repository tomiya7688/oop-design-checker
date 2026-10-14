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
        return ProductVersionResolver.Resolve(typeof(Program).Assembly);
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
