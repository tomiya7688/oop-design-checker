using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace OopDesignChecker.Gui;

internal sealed class App : Avalonia.Application
{
    internal static GuiTheme CurrentTheme =>
        Avalonia.Application.Current?.RequestedThemeVariant == ThemeVariant.Light
            ? GuiTheme.Light
            : GuiTheme.Dark;

    internal static void ApplyTheme(GuiTheme theme)
    {
        if (Avalonia.Application.Current is null)
        {
            return;
        }

        Avalonia.Application.Current.RequestedThemeVariant =
            theme == GuiTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Styles.Add(
            new StyleInclude(new Uri("avares://OopDesignChecker.Gui/"))
            {
                Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
            }
        );

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
