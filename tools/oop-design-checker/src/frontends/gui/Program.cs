using Avalonia;

namespace OopDesignChecker.Gui;

internal static class GUI起動処理
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--version" or "-V")
        {
            Console.WriteLine(製品バージョンを取得する());
            return;
        }

        Avaloniaアプリを構築する().StartWithClassicDesktopLifetime(args);
    }

    private static string 製品バージョンを取得する()
    {
        return ProductVersionResolver.Resolve(typeof(GUI起動処理).Assembly);
    }

    private static AppBuilder Avaloniaアプリを構築する() =>
        AppBuilder.Configure<GUIアプリ>().UsePlatformDetect();
}
