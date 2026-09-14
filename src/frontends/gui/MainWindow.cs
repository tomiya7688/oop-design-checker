using Avalonia;
using Avalonia.Controls;
using OopDesignChecker.Core;

namespace OopDesignChecker.Gui;

internal sealed class MainWindow : Window
{
    private readonly TextBox _targetPath = new();
    private readonly TextBox _configurationPath = new();
    private readonly Button _analyzeButton = new();
    private readonly TextBlock _status = new();
    private readonly ListBox _diagnostics = new();

    public MainWindow()
    {
        Title = "OOP Design Checker";
        Width = 1000;
        Height = 700;
        MinWidth = 700;
        MinHeight = 480;

        _targetPath.Text = Directory.GetCurrentDirectory();
        _targetPath.PlaceholderText = "Target project or C# file";
        _configurationPath.PlaceholderText = "Optional oop-design-checker.json path";
        _analyzeButton.Content = "Analyze";
        _analyzeButton.Click += async (_, _) => await AnalyzeAsync();
        _status.Text = "Ready";

        var controls = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock { Text = "Target" },
                _targetPath,
                new TextBlock { Text = "Configuration (optional)" },
                _configurationPath,
                _analyzeButton,
                _status,
                _diagnostics
            }
        };

        Content = controls;
    }

    private async Task AnalyzeAsync()
    {
        _analyzeButton.IsEnabled = false;
        _status.Text = "Analyzing...";

        try
        {
            var targetPath = _targetPath.Text?.Trim();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _status.Text = "Target path is required.";
                return;
            }

            var configurationPath = NormalizeOptionalPath(_configurationPath.Text);
            var result = await Task.Run(() => CheckerService.Analyze(targetPath, configurationPath));

            _diagnostics.ItemsSource = result.Diagnostics
                .Select(FormatDiagnostic)
                .ToArray();

            var dangerCount = result.Diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Danger);
            var warningCount = result.Diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Warning);
            var attentionCount = result.Diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Attention);
            _status.Text = $"Danger {dangerCount} / Warning {warningCount} / Attention {attentionCount}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _diagnostics.ItemsSource = Array.Empty<string>();
            _status.Text = exception.Message;
        }
        finally
        {
            _analyzeButton.IsEnabled = true;
        }
    }

    private static string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    private static string FormatDiagnostic(DesignDiagnostic diagnostic) =>
        $"{diagnostic.Severity.ToString().ToUpperInvariant()} {diagnostic.Rule.Id} "
        + $"{diagnostic.Location.FilePath}:{diagnostic.Location.Line}:{diagnostic.Location.Column} "
        + diagnostic.Message;
}
