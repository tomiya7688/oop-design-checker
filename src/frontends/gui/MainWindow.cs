using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using OopDesignChecker.Core;

namespace OopDesignChecker.Gui;

internal sealed class MainWindow : Window
{
    private readonly MainWindowView _view = new();
    private IReadOnlyList<DiagnosticRow> _allRows = [];
    private CheckerRunResult? _lastResult;
    private bool _analysisInProgress;

    public MainWindow()
    {
        Title = "OOP Design Checker";
        Width = 1280;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;
        Content = _view;

        WireEvents();
        UpdateSummary();
    }

    private void WireEvents()
    {
        _view.AnalyzeButton.Click += async (_, _) => await AnalyzeAsync();
        _view.TargetFileButton.Click += async (_, _) => await PickTargetFileAsync();
        _view.TargetFolderButton.Click += async (_, _) => await PickTargetFolderAsync();
        _view.ConfigurationButton.Click += async (_, _) => await PickConfigurationAsync();
        _view.ClearConfigurationButton.Click += (_, _) =>
            _view.ConfigurationPath.Text = string.Empty;
        _view.DangerFilter.Click += (_, _) => ApplyFilters();
        _view.WarningFilter.Click += (_, _) => ApplyFilters();
        _view.AttentionFilter.Click += (_, _) => ApplyFilters();
        _view.SearchFilter.TextChanged += (_, _) => ApplyFilters();
        _view.DiagnosticsGrid.SelectionChanged += (_, _) => UpdateSelectedDiagnostic();
        _view.DiagnosticsGrid.DoubleTapped += async (_, _) => await OpenSelectedSourceAsync();
        _view.CopySelectedButton.Click += async (_, _) => await CopySelectedAsync();
        _view.CopyAllButton.Click += async (_, _) => await CopyAllAsync();
        _view.OpenSourceButton.Click += async (_, _) => await OpenSelectedSourceAsync();
        KeyDown += OnKeyDown;
    }

    private async Task AnalyzeAsync()
    {
        if (_analysisInProgress)
        {
            return;
        }

        var targetPath = _view.TargetPath.Text?.Trim();
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            _view.Status.Text = "Target path is required.";
            return;
        }

        SetBusy(true);
        try
        {
            var configurationPath = NormalizeOptionalPath(_view.ConfigurationPath.Text);
            var result = await Task.Run(() =>
                CheckerService.Analyze(targetPath, configurationPath)
            );
            ShowResult(result);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            ShowError(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowResult(CheckerRunResult result)
    {
        _lastResult = result;
        _allRows = result.Diagnostics.Select(DiagnosticRow.From).ToArray();
        ApplyFilters();
        UpdateSummary();
        _view.Status.Text = $"Completed: {_allRows.Count} diagnostic(s).";
        _view.ConfigurationPath.Text = result.ConfigurationPath ?? _view.ConfigurationPath.Text;
    }

    private void ShowError(string message)
    {
        _lastResult = null;
        _allRows = [];
        _view.DiagnosticsGrid.ItemsSource = Array.Empty<DiagnosticRow>();
        _view.DiagnosticsGrid.SelectedItem = null;
        _view.ClearDetails();
        UpdateSummary();
        _view.Status.Text = message;
    }

    private void ApplyFilters()
    {
        var options = new DiagnosticFilterOptions(
            _view.DangerFilter.IsChecked == true,
            _view.WarningFilter.IsChecked == true,
            _view.AttentionFilter.IsChecked == true,
            _view.SearchFilter.Text ?? string.Empty
        );
        _view.DiagnosticsGrid.ItemsSource = DiagnosticFilter.Apply(_allRows, options);
    }

    private void UpdateSummary()
    {
        _view.DangerCount.Text = $"Danger: {Count(DesignDiagnosticSeverity.Danger)}";
        _view.WarningCount.Text = $"Warning: {Count(DesignDiagnosticSeverity.Warning)}";
        _view.AttentionCount.Text = $"Attention: {Count(DesignDiagnosticSeverity.Attention)}";

        if (_lastResult is null)
        {
            _view.ConfigurationSummary.Text = "Config: not loaded";
            return;
        }

        var configuration = _lastResult.Configuration;
        var configName = _lastResult.ConfigurationPath is null
            ? "defaults"
            : Path.GetFileName(_lastResult.ConfigurationPath);
        _view.ConfigurationSummary.Text =
            $"Config: {configName} | Fail: {_lastResult.FailureThreshold} | "
            + $"Disabled: {configuration.DisabledRules.Count} | Ignored: {configuration.IgnoredPaths.Count}";
    }

    private int Count(DesignDiagnosticSeverity severity) =>
        _allRows.Count(row => row.Severity == severity);

    private void UpdateSelectedDiagnostic()
    {
        if (_view.DiagnosticsGrid.SelectedItem is not DiagnosticRow row)
        {
            _view.ClearDetails();
            return;
        }

        _view.DetailSeverity.Text = $"Severity: {row.SeverityText}";
        _view.DetailRule.Text = $"Rule: {row.RuleId}";
        _view.DetailSymbol.Text = $"Symbol: {row.SymbolName ?? "-"}";
        _view.DetailLocation.Text = $"Location: {row.LocationText}";
        _view.DetailMessage.Text = row.Message;
    }

    private async Task PickTargetFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose analysis target",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("C# source/project/solution")
                    {
                        Patterns = ["*.cs", "*.csproj", "*.sln", "*.slnx"],
                    },
                ],
            }
        );
        SetPathFromSelection(files.FirstOrDefault(), _view.TargetPath);
    }

    private async Task PickTargetFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose project directory",
                AllowMultiple = false,
            }
        );
        SetPathFromSelection(folders.FirstOrDefault(), _view.TargetPath);
    }

    private async Task PickConfigurationAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose checker configuration",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("JSON configuration") { Patterns = ["*.json"] },
                ],
            }
        );
        SetPathFromSelection(files.FirstOrDefault(), _view.ConfigurationPath);
    }

    private static void SetPathFromSelection(IStorageItem? item, TextBox destination)
    {
        var path = item?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            destination.Text = path;
        }
    }

    private async Task CopySelectedAsync()
    {
        if (_view.DiagnosticsGrid.SelectedItem is DiagnosticRow row)
        {
            await CopyTextAsync(row.CopyText);
        }
    }

    private async Task CopyAllAsync()
    {
        if (_allRows.Count == 0)
        {
            return;
        }

        await CopyTextAsync(string.Join(Environment.NewLine, _allRows.Select(row => row.CopyText)));
    }

    private async Task CopyTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _view.Status.Text = "Clipboard is unavailable on this platform.";
            return;
        }

        await clipboard.SetTextAsync(text);
        _view.Status.Text = "Copied to clipboard.";
    }

    private async Task OpenSelectedSourceAsync()
    {
        if (_view.DiagnosticsGrid.SelectedItem is not DiagnosticRow row)
        {
            return;
        }

        if (!File.Exists(row.FilePath))
        {
            await CopyTextAsync(row.LocationText);
            _view.Status.Text = "Source file was not found; location copied instead.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(row.FilePath) { UseShellExecute = true });
            _view.Status.Text = "Opened source file with the default application.";
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            await CopyTextAsync(row.LocationText);
            _view.Status.Text = "Could not open source file; location copied instead.";
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (
            e.Key == Key.F5
            || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        )
        {
            e.Handled = true;
            await AnalyzeAsync();
            return;
        }

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            _view.SearchFilter.Focus();
            return;
        }

        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            await CopySelectedAsync();
        }
    }

    private void SetBusy(bool isBusy)
    {
        _analysisInProgress = isBusy;
        _view.AnalyzeButton.IsEnabled = !isBusy;
        _view.TargetFileButton.IsEnabled = !isBusy;
        _view.TargetFolderButton.IsEnabled = !isBusy;
        _view.ConfigurationButton.IsEnabled = !isBusy;
        _view.Progress.IsVisible = isBusy;
        _view.Status.Text = isBusy ? "Analyzing..." : _view.Status.Text;
    }

    private static string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();
}
