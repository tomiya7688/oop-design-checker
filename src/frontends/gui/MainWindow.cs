using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Gui;

internal sealed class MainWindow : Window
{
    private readonly MainWindowView _view = new();
    private DiagnosticRow[] _allRows = [];
    private CheckerRunResult? _lastResult;
    private CancellationTokenSource? _analysisCancellation;
    private UserInterfaceLanguage _language = UserInterfaceLanguage.Japanese;
    private bool _analysisInProgress;

    public MainWindow()
    {
        Title = GuiText.Get(GuiTextKey.WindowTitle, _language);
        Width = 1280;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;
        Content = _view;

        WireEvents();
        UpdateSummary();
        _view.Status.Text = GuiText.Get(GuiTextKey.Ready, _language);
    }

    internal MainWindowView TestView => _view;

    internal void PresentResultForTesting(CheckerRunResult result) => ShowResult(result);

    internal void PresentErrorForTesting(string message) => ShowError(message);

    internal void ApplyFiltersForTesting() => ApplyFilters();

    internal void UpdateSelectedDiagnosticForTesting() => UpdateSelectedDiagnostic();

    internal void SetLanguageForTesting(UserInterfaceLanguage language)
    {
        _view.LanguageSelector.SelectedIndex =
            language == UserInterfaceLanguage.English ? 1 : 0;
        ChangeLanguage();
    }

    private void WireEvents()
    {
        _view.AnalyzeButton.Click += async (_, _) => await AnalyzeAsync();
        _view.CancelButton.Click += (_, _) => CancelAnalysis();
        _view.TargetFileButton.Click += async (_, _) => await PickTargetFileAsync();
        _view.TargetFolderButton.Click += async (_, _) => await PickTargetFolderAsync();
        _view.ConfigurationButton.Click += async (_, _) => await PickConfigurationAsync();
        _view.EditConfigurationButton.Click += async (_, _) => await EditConfigurationAsync();
        _view.ClearConfigurationButton.Click += (_, _) =>
            _view.ConfigurationPath.Text = string.Empty;
        _view.DangerFilter.Click += (_, _) => ApplyFilters();
        _view.WarningFilter.Click += (_, _) => ApplyFilters();
        _view.AttentionFilter.Click += (_, _) => ApplyFilters();
        _view.SearchFilter.TextChanged += (_, _) => ApplyFilters();
        _view.LanguageSelector.SelectionChanged += (_, _) => ChangeLanguage();
        _view.DiagnosticsGrid.SelectionChanged += (_, _) => UpdateSelectedDiagnostic();
        _view.DiagnosticsGrid.DoubleTapped += async (_, _) => await OpenSelectedSourceAsync();
        _view.CopySelectedButton.Click += async (_, _) => await CopySelectedAsync();
        _view.CopyAllButton.Click += async (_, _) => await CopyAllAsync();
        _view.OpenSourceButton.Click += async (_, _) => await OpenSelectedSourceAsync();
        _view.ExportJsonButton.Click += async (_, _) =>
            await ExportAsync(DiagnosticExportFormat.Json);
        _view.ExportSarifButton.Click += async (_, _) =>
            await ExportAsync(DiagnosticExportFormat.Sarif);
        KeyDown += OnKeyDown;
    }

    private void ChangeLanguage()
    {
        _language =
            _view.LanguageSelector.SelectedIndex == 1
                ? UserInterfaceLanguage.English
                : UserInterfaceLanguage.Japanese;
        Title = GuiText.Get(GuiTextKey.WindowTitle, _language);
        _view.ApplyLanguage(_language);
        if (_lastResult is not null)
        {
            _allRows = _lastResult
                .Diagnostics.Select(diagnostic => DiagnosticRow.From(diagnostic, _language))
                .ToArray();
            ApplyFilters();
        }

        UpdateSummary();
        UpdateSelectedDiagnostic();
        UpdateLocalizedStatus();
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
            _view.Status.Text = GuiText.Get(GuiTextKey.TargetRequired, _language);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _analysisCancellation = cancellation;
        SetBusy(true);

        try
        {
            var configurationPath = NormalizeOptionalPath(_view.ConfigurationPath.Text);
            var result = await Task.Run(
                () =>
                    CheckerService.Analyze(
                        targetPath,
                        configurationPath,
                        cancellationToken: cancellation.Token
                    ),
                cancellation.Token
            );
            ShowResult(result);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _view.Status.Text = GuiText.Get(GuiTextKey.AnalysisCancelled, _language);
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
            if (ReferenceEquals(_analysisCancellation, cancellation))
            {
                _analysisCancellation = null;
            }

            SetBusy(false);
        }
    }

    private void CancelAnalysis()
    {
        if (_analysisCancellation is null || _analysisCancellation.IsCancellationRequested)
        {
            return;
        }

        _analysisCancellation.Cancel();
        _view.CancelButton.IsEnabled = false;
        _view.Status.Text = GuiText.Get(GuiTextKey.Cancelling, _language);
    }

    private void ShowResult(CheckerRunResult result)
    {
        _lastResult = result;
        _allRows = result
            .Diagnostics.Select(diagnostic => DiagnosticRow.From(diagnostic, _language))
            .ToArray();
        ApplyFilters();
        UpdateSummary();
        _view.Status.Text = GuiText.Format(GuiTextKey.Completed, _language, _allRows.Length);
        _view.ConfigurationPath.Text = result.ConfigurationPath ?? _view.ConfigurationPath.Text;
    }

    private void ShowError(string message)
    {
        _lastResult = null;
        _allRows = [];
        _view.DiagnosticsGrid.ItemsSource = Array.Empty<DiagnosticRow>();
        _view.DiagnosticsGrid.SelectedItem = null;
        _view.ClearDetails(_language);
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
        _view.DangerCount.Text =
            $"{GuiText.Get(GuiTextKey.Danger, _language)}: {Count(DesignDiagnosticSeverity.Danger)}";
        _view.WarningCount.Text =
            $"{GuiText.Get(GuiTextKey.Warning, _language)}: {Count(DesignDiagnosticSeverity.Warning)}";
        _view.AttentionCount.Text =
            $"{GuiText.Get(GuiTextKey.Attention, _language)}: {Count(DesignDiagnosticSeverity.Attention)}";

        if (_lastResult is null)
        {
            _view.ConfigurationSummary.Text = GuiText.Get(GuiTextKey.ConfigNotLoaded, _language);
            return;
        }

        var configuration = _lastResult.Configuration;
        var configName = _lastResult.ConfigurationPath is null
            ? GuiText.Get(GuiTextKey.ConfigDefaults, _language)
            : Path.GetFileName(_lastResult.ConfigurationPath);
        _view.ConfigurationSummary.Text = GuiText.Format(
            GuiTextKey.ConfigSummary,
            _language,
            configName,
            LocalizedText.SeverityName(_language, _lastResult.FailureThreshold),
            configuration.DisabledRules.Count,
            configuration.IgnoredPaths.Count
        );
    }

    private int Count(DesignDiagnosticSeverity severity) =>
        _allRows.Count(row => row.Severity == severity);

    private void UpdateSelectedDiagnostic()
    {
        if (_view.DiagnosticsGrid.SelectedItem is not DiagnosticRow row)
        {
            _view.ClearDetails(_language);
            return;
        }

        _view.DetailSeverity.Text = GuiText.Format(
            GuiTextKey.SeverityDetail,
            _language,
            LocalizedText.SeverityName(_language, row.Severity)
        );
        _view.DetailRule.Text = GuiText.Format(
            GuiTextKey.RuleDetail,
            _language,
            $"{row.RuleId} — {row.RuleTitle}"
        );
        _view.DetailSymbol.Text = GuiText.Format(
            GuiTextKey.SymbolDetail,
            _language,
            row.SymbolName ?? "-"
        );
        _view.DetailLocation.Text = GuiText.Format(
            GuiTextKey.LocationDetail,
            _language,
            row.LocationText
        );
        _view.DetailMessage.Text = row.Message;
    }

    private async Task PickTargetFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = GuiText.Get(GuiTextKey.ChooseAnalysisTarget, _language),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(GuiText.Get(GuiTextKey.CSharpTargetType, _language))
                    {
                        Patterns = ["*.cs", "*.csproj", "*.sln", "*.slnx"],
                    },
                ],
            }
        );
        SetPathFromSelection(files.Count == 0 ? null : files[0], _view.TargetPath);
    }

    private async Task PickTargetFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = GuiText.Get(GuiTextKey.ChooseProjectDirectory, _language),
                AllowMultiple = false,
            }
        );
        SetPathFromSelection(folders.Count == 0 ? null : folders[0], _view.TargetPath);
    }

    private async Task PickConfigurationAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = GuiText.Get(GuiTextKey.ChooseCheckerConfiguration, _language),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(GuiText.Get(GuiTextKey.JsonConfigurationType, _language))
                    {
                        Patterns = ["*.json"],
                    },
                ],
            }
        );
        SetPathFromSelection(files.Count == 0 ? null : files[0], _view.ConfigurationPath);
    }

    private async Task EditConfigurationAsync()
    {
        if (_analysisInProgress)
        {
            return;
        }

        try
        {
            var configurationPath = ResolveConfigurationEditorPath();
            var initialJson =
                configurationPath is not null && File.Exists(configurationPath)
                    ? await File.ReadAllTextAsync(configurationPath)
                    : CheckerConfigurationJson.Serialize(
                        _lastResult?.Configuration ?? new CheckerConfiguration()
                    );

            var editor = new ConfigurationEditorWindow(initialJson, _language);
            var editedJson = await editor.ShowDialog<string?>(this);
            if (editedJson is null)
            {
                return;
            }

            _ = CheckerConfigurationJson.Parse(editedJson);
            configurationPath ??= await PickConfigurationSavePathAsync();
            if (configurationPath is null)
            {
                return;
            }

            await File.WriteAllTextAsync(configurationPath, editedJson);
            _view.ConfigurationPath.Text = configurationPath;
            _view.Status.Text = GuiText.Get(GuiTextKey.ConfigurationSaved, _language);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            _view.Status.Text = exception.Message;
        }
    }

    private string? ResolveConfigurationEditorPath()
    {
        var configuredPath = NormalizeOptionalPath(_view.ConfigurationPath.Text);
        if (configuredPath is null)
        {
            return _lastResult?.ConfigurationPath;
        }

        return CheckerConfigurationPathResolver.ResolveExplicit(
            NormalizeOptionalPath(_view.TargetPath.Text),
            configuredPath
        );
    }

    private async Task<string?> PickConfigurationSavePathAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = GuiText.Get(GuiTextKey.SaveCheckerConfiguration, _language),
                SuggestedFileName = "oop-design-checker.json",
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType(GuiText.Get(GuiTextKey.JsonConfigurationType, _language))
                    {
                        Patterns = ["*.json"],
                    },
                ],
            }
        );
        return file?.TryGetLocalPath();
    }

    private async Task ExportAsync(DiagnosticExportFormat format)
    {
        if (_lastResult is null)
        {
            _view.Status.Text = GuiText.Get(GuiTextKey.RunBeforeExport, _language);
            return;
        }

        var extension = format == DiagnosticExportFormat.Sarif ? "sarif" : "json";
        var description = format == DiagnosticExportFormat.Sarif ? "SARIF" : "JSON";
        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = GuiText.Format(GuiTextKey.ExportDiagnosticsAs, _language, description),
                SuggestedFileName = $"oop-design-checker-results.{extension}",
                DefaultExtension = extension,
                FileTypeChoices =
                [
                    new FilePickerFileType(
                        GuiText.Format(GuiTextKey.DiagnosticsFileType, _language, description)
                    )
                    {
                        Patterns = [$"*.{extension}"],
                    },
                ],
            }
        );
        var outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            await Task.Run(() =>
                DiagnosticExportService.Export(_lastResult.Diagnostics, outputPath, format)
            );
            _view.Status.Text = GuiText.Format(
                GuiTextKey.ExportedDiagnostics,
                _language,
                outputPath
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _view.Status.Text = exception.Message;
        }
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
        if (_allRows.Length == 0)
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
            _view.Status.Text = GuiText.Get(GuiTextKey.ClipboardUnavailable, _language);
            return;
        }

        var copied = await ClipboardOperation.TrySetTextAsync(() => clipboard.SetTextAsync(text));
        _view.Status.Text = GuiText.Get(
            copied ? GuiTextKey.CopiedToClipboard : GuiTextKey.ClipboardCopyFailed,
            _language
        );
    }

    private async Task OpenSelectedSourceAsync()
    {
        if (_view.DiagnosticsGrid.SelectedItem is not DiagnosticRow row)
        {
            return;
        }

        string sourcePath;
        try
        {
            sourcePath = Path.GetFullPath(row.FilePath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or IOException)
        {
            await CopySourceLocationFallbackAsync(row.LocationText);
            _view.Status.Text = GuiText.Get(GuiTextKey.InvalidSourcePathCopied, _language);
            return;
        }

        if (!File.Exists(sourcePath))
        {
            await CopySourceLocationFallbackAsync(row.LocationText);
            _view.Status.Text = GuiText.Get(GuiTextKey.SourceNotFoundCopied, _language);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(sourcePath) { UseShellExecute = true });
            _view.Status.Text = GuiText.Get(GuiTextKey.OpenedSource, _language);
        }
        catch (Exception exception)
            when (exception
                    is InvalidOperationException
                        or System.ComponentModel.Win32Exception
                        or IOException
                        or UnauthorizedAccessException
            )
        {
            await CopySourceLocationFallbackAsync(row.LocationText);
            _view.Status.Text = GuiText.Get(GuiTextKey.CouldNotOpenCopied, _language);
        }
    }

    private async Task CopySourceLocationFallbackAsync(string locationText)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            _ = await ClipboardOperation.TrySetTextAsync(() =>
                clipboard.SetTextAsync(locationText)
            );
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _analysisInProgress)
        {
            e.Handled = true;
            CancelAnalysis();
            return;
        }

        if (e.Key == Key.F5 || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
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
        _view.CancelButton.IsEnabled = isBusy;
        _view.TargetFileButton.IsEnabled = !isBusy;
        _view.TargetFolderButton.IsEnabled = !isBusy;
        _view.ConfigurationButton.IsEnabled = !isBusy;
        _view.EditConfigurationButton.IsEnabled = !isBusy;
        _view.ExportJsonButton.IsEnabled = !isBusy && _lastResult is not null;
        _view.ExportSarifButton.IsEnabled = !isBusy && _lastResult is not null;
        _view.LanguageSelector.IsEnabled = !isBusy;
        _view.Progress.IsVisible = isBusy;
        if (isBusy)
        {
            _view.Status.Text = GuiText.Get(GuiTextKey.Analyzing, _language);
        }
    }

    private void UpdateLocalizedStatus()
    {
        if (_analysisInProgress)
        {
            _view.Status.Text = GuiText.Get(GuiTextKey.Analyzing, _language);
            return;
        }

        _view.Status.Text = _lastResult is null
            ? GuiText.Get(GuiTextKey.Ready, _language)
            : GuiText.Format(GuiTextKey.Completed, _language, _allRows.Length);
    }

    private static string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();
}
