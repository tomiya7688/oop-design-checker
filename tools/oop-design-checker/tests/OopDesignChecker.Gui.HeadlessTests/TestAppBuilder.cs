using Avalonia;
using Avalonia.Headless;
using OopDesignChecker.Gui;

[assembly: Avalonia.Headless.AvaloniaTestApplication(
    typeof(OopDesignChecker.Gui.HeadlessTests.GUIテスト用アプリ構築器)
)]

namespace OopDesignChecker.Gui.HeadlessTests;

public static class GUIテスト用アプリ構築器
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<GUIアプリ>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
