using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace OopDesignChecker.Gui;

internal sealed class App : Avalonia.Application
{
    public override void OnFrameworkInitializationCompleted()
    {
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
