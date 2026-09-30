using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
        UpdateActionAvailability();
    }

    internal MainWindowView TestView => _view;

    internal void PresentResultForTesting(CheckerRunResult result) => ShowResult(result);

    internal void PresentErrorForTesting(string message) => ShowError(message);

    internal void ApplyFiltersForTesting() => ApplyFilters();

    internal void UpdateSelectedDiagnosticForTesting() => UpdateSelectedDiagnostic();

    internal void SetLanguageForTesting(UserInterfaceLanguage language)
    {
        _view.LanguageSelector.SelectedIndex = language == UserInterfaceLanguage.English ? 1 : 0;
        ChangeLanguage();
    }

    internal void SetThemeForTesting(ThemeVariant theme)
    {
        _view.ThemeSelector.SelectedIndex = theme == ThemeVariant.Light ? 1 : 0;
        ChangeTheme();
    }

    private void WireEvents()
    {
        _view.AnalyzeButton.Click += async (_, _) => await AnalyzeAsync();
        _view.CancelButton.Click += (_, _) => CancelAnalysis();
        _view.TargetFileButton.Click += async (_, _) => await PickTargetFileAsync();
        _view.TargetFolderButton.Click += async (_, _) => await PickTargetFolderAsync();
        _view.ConfigurationButton.Click += async (_, _) => await PickConfigurationAsync();
        _view.EditConfigurationButton.Click += async (_, _) => await EditConfigurationAsync();
        _view.ClearConfigurationButton.Click += (_, _) => ClearConfiguration();
        _view.DangerFilter.Click += (_, _) => FilterStateChanged();
        _view.WarningFilter.Click += (_, _) => FilterStateChanged();
        _view.AttentionFilter.Click += (_, _) => FilterStateChanged();
        _view.SearchFilter.TextChanged += (_, _) => ApplyFilters();
        _view.LanguageSelector.SelectionChanged += (_, _) => ChangeLanguage();
        _view.ThemeSelector.SelectionChanged += (_, _) => ChangeTheme();
        _view.DiagnosticsGrid.SelectionChanged += (_, _) => UpdateSelectedDiagnostic();
        _view.DiagnosticsGrid.DoubleTapped += async (_, _) => await OpenSelectedSourceAsync();
        _view.CopySelectedButton.Click += async (_, _) => await CopySelectedAsync();
        _view.CopyAllButton.Click += async (_, _) => await CopyAllAsync();
        _view.OpenSourceButton.Click += async (_, _) => await OpenSelectedSourceAsync();
        _view.ExportJsonButton.Click += async (_, _) =>
            await ExportAsync(DiagnosticExportFormat.Json);
        _view.ExportSarifButton.Click += async (_, _) =>
            await ExportAsync(DiagnosticExportFormat.Sarif);

        _view.MainMenu.TargetFileItem.Click += async (_, _) => await PickTargetFileAsync();
        _view.MainMenu.TargetFolderItem.Click += async (_, _) => await PickTargetFolderAsync();
        _view.MainMenu.ConfigurationItem.Click += async (_, _) => await PickConfigurationAsync();
        _view.MainMenu.EditConfigurationItem.Click += async (_, _) =>
            await EditConfigurationAsync();
        _view.MainMenu.ClearConfigurationItem.Click += (_, _) => ClearConfiguration();
        _view.MainMenu.ExportJsonItem.Click += async (_, _) =>
            await ExportAsync(DiagnosticExportFormat.Json);
        _view.MainMenu.ExportSarifItem.Click += async (_, _) =>
            await ExportAsync(DiagnosticExportFormat.Sarif);
        _view.MainMenu.ExitItem.Click += (_, _) => Close();

        _view.MainMenu.AnalyzeItem.Click += async (_, _) => await AnalyzeAsync();
        _view.MainMenu.CancelItem.Click += (_, _) => CancelAnalysis();
        _view.MainMenu.OpenSourceItem.Click += async (_, _) => await OpenSelectedSourceAsync();

        _view.MainMenu.SearchItem.Click += (_, _) => FocusSearch();
        _view.MainMenu.DangerFilterItem.Click += (_, _) => ToggleFilterFromMenu(_view.DangerFilter);
        _view.MainMenu.WarningFilterItem.Click += (_, _) =>
            ToggleFilterFromMenu(_view.WarningFilter);
        _view.MainMenu.AttentionFilterItem.Click += (_, _) =>
            ToggleFilterFromMenu(_view.AttentionFilter);
        _view.MainMenu.JapaneseLanguageItem.Click += (_, _) =>
            _view.LanguageSelector.SelectedIndex = 0;
        _view.MainMenu.EnglishLanguageItem.Click += (_, _) =>
            _view.LanguageSelector.SelectedIndex = 1;
        _view.MainMenu.DarkThemeItem.Click += (_, _) => _view.ThemeSelector.SelectedIndex = 0;
        _view.MainMenu.LightThemeItem.Click += (_, _) => _view.ThemeSelector.SelectedIndex = 1;

        _view.TargetPath.KeyDown += OnEditableKeyDown;
        _view.ConfigurationPath.KeyDown += OnEditableKeyDown;
        _view.SearchFilter.KeyDown += OnEditableKeyDown;
        AddHandler(
            InputElement.KeyDownEvent,
            OnPreviewKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true
        );
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
        UpdateActionAvailability();
    }

    private void ChangeTheme()
    {
        if (Avalonia.Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant =
            _view.ThemeSelector.SelectedIndex == 1 ? ThemeVariant.Light : ThemeVariant.Dark;
        UpdateActionAvailability();
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
            var automationDelay = UiAutomationSettings.AnalysisDelayMilliseconds;
            if (automationDelay > 0)
            {
                await Task.Delay(automationDelay, cancellation.Token);
            }

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
        _view.Status.Text = GuiText.Get(GuiTextKey.Cancelling, _language);
        UpdateActionAvailability();
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
        UpdateActionAvailability();
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
        UpdateActionAvailability();
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
        SyncMenuState();
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
            UpdateActionAvailability();
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
        UpdateActionAvailability();
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
        var outputPath = UiAutomationSettings.ExportPath(format);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
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
            outputPath = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }
        }

        try
        {
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

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

    private void ClearConfiguration()
    {
        _view.ConfigurationPath.Text = string.Empty;
    }

    private void FilterStateChanged()
    {
        ApplyFilters();
        SyncMenuState();
    }

    private void ToggleFilterFromMenu(CheckBox filter)
    {
        filter.IsChecked = filter.IsChecked != true;
        FilterStateChanged();
    }

    private void FocusSearch()
    {
        _view.SearchFilter.Focus();
    }

    private void OnEditableKeyDown(object? sender, KeyEventArgs e)
    {
        if (!GuiCommands.Cancel.Matches(e))
        {
            return;
        }

        e.Handled = true;
        if (_analysisInProgress)
        {
            _view.CancelButton.Focus();
        }
        else
        {
            _view.AnalyzeButton.Focus();
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (GuiCommands.Cancel.Matches(e) && AnyTopLevelMenuOpen())
        {
            CloseTopLevelMenus();
            e.Handled = true;
            return;
        }

        if (
            e.KeyModifiers == KeyModifiers.None
            && AnyTopLevelMenuOpen()
            && TryHandleOpenMenuAccessKey(e.Key)
        )
        {
            e.Handled = true;
            return;
        }

        if (TryHandleAltAccessKey(e))
        {
            e.Handled = true;
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (GuiCommands.Cancel.Matches(e) && _analysisInProgress)
        {
            e.Handled = true;
            CancelAnalysis();
            return;
        }

        if (GuiCommands.Analyze.Matches(e))
        {
            e.Handled = true;
            await AnalyzeAsync();
            return;
        }

        if (GuiCommands.Search.Matches(e))
        {
            e.Handled = true;
            FocusSearch();
            return;
        }

        if (GuiCommands.CopySelected.Matches(e))
        {
            if (FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            e.Handled = true;
            await CopySelectedAsync();
        }
    }

    private bool TryHandleOpenMenuAccessKey(Key key)
    {
        var menu = _view.MainMenu;

        if (menu.LanguageMenu.IsSubMenuOpen)
        {
            if (MatchesMenuAccessKey(GuiCommands.JapaneseLanguage, key))
            {
                CloseTopLevelMenus();
                _view.LanguageSelector.SelectedIndex = 0;
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.EnglishLanguage, key))
            {
                CloseTopLevelMenus();
                _view.LanguageSelector.SelectedIndex = 1;
                return true;
            }
        }

        if (menu.ThemeMenu.IsSubMenuOpen)
        {
            if (MatchesMenuAccessKey(GuiCommands.DarkTheme, key))
            {
                CloseTopLevelMenus();
                _view.ThemeSelector.SelectedIndex = 0;
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.LightTheme, key))
            {
                CloseTopLevelMenus();
                _view.ThemeSelector.SelectedIndex = 1;
                return true;
            }
        }

        if (menu.FileMenu.IsSubMenuOpen)
        {
            if (MatchesMenuAccessKey(GuiCommands.TargetFile, key))
            {
                CloseTopLevelMenus();
                _ = PickTargetFileAsync();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.TargetFolder, key))
            {
                CloseTopLevelMenus();
                _ = PickTargetFolderAsync();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.PickConfiguration, key))
            {
                CloseTopLevelMenus();
                _ = PickConfigurationAsync();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.EditConfiguration, key))
            {
                CloseTopLevelMenus();
                _ = EditConfigurationAsync();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.ClearConfiguration, key))
            {
                CloseTopLevelMenus();
                ClearConfiguration();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.ExportJson, key))
            {
                CloseTopLevelMenus();
                _ = ExportAsync(DiagnosticExportFormat.Json);
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.ExportSarif, key))
            {
                CloseTopLevelMenus();
                _ = ExportAsync(DiagnosticExportFormat.Sarif);
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.Exit, key))
            {
                Close();
                return true;
            }
        }

        if (menu.AnalyzeMenu.IsSubMenuOpen)
        {
            if (MatchesMenuAccessKey(GuiCommands.Analyze, key))
            {
                CloseTopLevelMenus();
                _ = AnalyzeAsync();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.Cancel, key))
            {
                CloseTopLevelMenus();
                CancelAnalysis();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.OpenSource, key))
            {
                CloseTopLevelMenus();
                _ = OpenSelectedSourceAsync();
                return true;
            }
        }

        if (menu.ViewMenu.IsSubMenuOpen)
        {
            if (MatchesMenuAccessKey(GuiCommands.Search, key))
            {
                CloseTopLevelMenus();
                FocusSearch();
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.DangerFilter, key))
            {
                CloseTopLevelMenus();
                ToggleFilterFromMenu(_view.DangerFilter);
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.WarningFilter, key))
            {
                CloseTopLevelMenus();
                ToggleFilterFromMenu(_view.WarningFilter);
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.AttentionFilter, key))
            {
                CloseTopLevelMenus();
                ToggleFilterFromMenu(_view.AttentionFilter);
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.LanguageMenu, key))
            {
                OpenSubMenu(menu.LanguageMenu);
                return true;
            }

            if (MatchesMenuAccessKey(GuiCommands.ThemeMenu, key))
            {
                OpenSubMenu(menu.ThemeMenu);
                return true;
            }
        }

        return false;
    }

    private static void OpenSubMenu(MenuItem menu)
    {
        menu.Open();
    }

    private bool TryHandleAltAccessKey(KeyEventArgs e)
    {
        if (MatchesTopLevelAccessKey(GuiCommands.FileMenu, e))
        {
            OpenTopLevelMenu(_view.MainMenu.FileMenu);
            return true;
        }

        if (MatchesTopLevelAccessKey(GuiCommands.AnalyzeMenu, e))
        {
            OpenTopLevelMenu(_view.MainMenu.AnalyzeMenu);
            return true;
        }

        if (MatchesTopLevelAccessKey(GuiCommands.ViewMenu, e))
        {
            OpenTopLevelMenu(_view.MainMenu.ViewMenu);
            return true;
        }

        if (GuiCommands.TargetFocus.Matches(e))
        {
            _view.TargetPath.Focus();
            return true;
        }

        if (GuiCommands.ConfigurationFocus.Matches(e))
        {
            _view.ConfigurationPath.Focus();
            return true;
        }

        return false;
    }

    private static bool MatchesMenuAccessKey(GuiCommandDefinition command, Key key) =>
        command.MenuAccessKey == key;

    private static bool MatchesTopLevelAccessKey(GuiCommandDefinition command, KeyEventArgs e) =>
        e.KeyModifiers == KeyModifiers.Alt
        && command.MenuPath.Count == 1
        && command.MenuPath[0] == e.Key;

    private bool AnyTopLevelMenuOpen() =>
        _view.MainMenu.FileMenu.IsSubMenuOpen
        || _view.MainMenu.AnalyzeMenu.IsSubMenuOpen
        || _view.MainMenu.ViewMenu.IsSubMenuOpen;

    private void CloseTopLevelMenus()
    {
        _view.MainMenu.FileMenu.IsSubMenuOpen = false;
        _view.MainMenu.AnalyzeMenu.IsSubMenuOpen = false;
        _view.MainMenu.ViewMenu.IsSubMenuOpen = false;
    }

    private void OpenTopLevelMenu(MenuItem menu)
    {
        CloseTopLevelMenus();
        menu.Open();
    }

    private void SetBusy(bool isBusy)
    {
        _analysisInProgress = isBusy;
        _view.Progress.IsVisible = isBusy;
        UpdateActionAvailability();
        if (isBusy)
        {
            _view.Status.Text = GuiText.Get(GuiTextKey.Analyzing, _language);
        }
    }

    private void UpdateActionAvailability()
    {
        var canEdit = !_analysisInProgress;
        var canCancel =
            _analysisInProgress && _analysisCancellation is { IsCancellationRequested: false };
        var hasResult = _lastResult is not null;
        var hasSelection = _view.DiagnosticsGrid.SelectedItem is DiagnosticRow;

        _view.AnalyzeButton.IsEnabled = canEdit;
        _view.CancelButton.IsEnabled = canCancel;
        _view.TargetFileButton.IsEnabled = canEdit;
        _view.TargetFolderButton.IsEnabled = canEdit;
        _view.ConfigurationButton.IsEnabled = canEdit;
        _view.EditConfigurationButton.IsEnabled = canEdit;
        _view.ClearConfigurationButton.IsEnabled = canEdit;
        _view.ExportJsonButton.IsEnabled = canEdit && hasResult;
        _view.ExportSarifButton.IsEnabled = canEdit && hasResult;
        _view.OpenSourceButton.IsEnabled = canEdit && hasSelection;
        _view.CopySelectedButton.IsEnabled = hasSelection;
        _view.CopyAllButton.IsEnabled = hasResult;
        _view.LanguageSelector.IsEnabled = canEdit;
        _view.ThemeSelector.IsEnabled = canEdit;

        var menu = _view.MainMenu;
        menu.TargetFileItem.IsEnabled = canEdit;
        menu.TargetFolderItem.IsEnabled = canEdit;
        menu.ConfigurationItem.IsEnabled = canEdit;
        menu.EditConfigurationItem.IsEnabled = canEdit;
        menu.ClearConfigurationItem.IsEnabled = canEdit;
        menu.ExportJsonItem.IsEnabled = canEdit && hasResult;
        menu.ExportSarifItem.IsEnabled = canEdit && hasResult;
        menu.AnalyzeItem.IsEnabled = canEdit;
        menu.CancelItem.IsEnabled = canCancel;
        menu.OpenSourceItem.IsEnabled = canEdit && hasSelection;
        menu.LanguageMenu.IsEnabled = canEdit;
        menu.ThemeMenu.IsEnabled = canEdit;

        SyncMenuState();
    }

    private void SyncMenuState()
    {
        var menu = _view.MainMenu;
        menu.DangerFilterItem.IsChecked = _view.DangerFilter.IsChecked == true;
        menu.WarningFilterItem.IsChecked = _view.WarningFilter.IsChecked == true;
        menu.AttentionFilterItem.IsChecked = _view.AttentionFilter.IsChecked == true;
        menu.JapaneseLanguageItem.IsChecked = _view.LanguageSelector.SelectedIndex != 1;
        menu.EnglishLanguageItem.IsChecked = _view.LanguageSelector.SelectedIndex == 1;
        menu.DarkThemeItem.IsChecked = _view.ThemeSelector.SelectedIndex != 1;
        menu.LightThemeItem.IsChecked = _view.ThemeSelector.SelectedIndex == 1;
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
