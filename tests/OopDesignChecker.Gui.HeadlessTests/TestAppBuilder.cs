using Avalonia;
using Avalonia.Headless;
using OopDesignChecker.Gui;

[assembly: Avalonia.Headless.XUnit.AvaloniaTestApplication(
    typeof(OopDesignChecker.Gui.HeadlessTests.TestAppBuilder)
)]

namespace OopDesignChecker.Gui.HeadlessTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
