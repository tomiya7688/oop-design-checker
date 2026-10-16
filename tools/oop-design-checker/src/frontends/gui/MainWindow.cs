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

internal sealed class メインウィンドウ : Window
{
    private readonly メイン画面 _画面 = new();
    private DiagnosticRow[] _全診断行 = [];
    private CheckerRunResult? _前回解析結果;
    private CancellationTokenSource? _解析キャンセル;
    private UserInterfaceLanguage _表示言語 = UserInterfaceLanguage.Japanese;
    private bool _解析中;

    public メインウィンドウ()
    {
        Title = GUI文言.取得する(GUI文言キー.ウィンドウタイトル, _表示言語);
        Width = 1280;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;
        Content = _画面;

        イベントを接続する();
        集計を更新する();
        _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.準備完了, _表示言語);
        操作の有効状態を更新する();
    }

    internal メイン画面 テスト用画面 => _画面;

    internal void テスト用解析結果を表示する(CheckerRunResult result) => 解析結果を表示する(result);

    internal void テスト用エラーを表示する(string message) => エラーを表示する(message);

    internal void テスト用絞り込みを適用する() => 絞り込みを適用する();

    internal void テスト用選択診断を更新する() => 選択した診断を更新する();

    internal void テスト用表示言語を設定する(UserInterfaceLanguage language)
    {
        _画面.表示言語選択.SelectedIndex = language == UserInterfaceLanguage.English ? 1 : 0;
        表示言語を変更する();
    }

    internal void テスト用テーマを設定する(ThemeVariant theme)
    {
        _画面.テーマ選択.SelectedIndex = theme == ThemeVariant.Light ? 1 : 0;
        テーマを変更する();
    }

    private void イベントを接続する()
    {
        _画面.解析ボタン.Click += async (_, _) => await 解析する非同期();
        _画面.キャンセルボタン.Click += (_, _) => 解析をキャンセルする();
        _画面.対象ファイルボタン.Click += async (_, _) => await 解析対象ファイルを選択する非同期();
        _画面.対象フォルダーボタン.Click += async (_, _) =>
            await 解析対象フォルダーを選択する非同期();
        _画面.設定選択ボタン.Click += async (_, _) => await 設定ファイルを選択する非同期();
        _画面.設定編集ボタン.Click += async (_, _) => await 設定を編集する非同期();
        _画面.設定消去ボタン.Click += (_, _) => 設定消去();
        _画面.危険絞り込み.Click += (_, _) => 絞り込み状態が変更された();
        _画面.警告絞り込み.Click += (_, _) => 絞り込み状態が変更された();
        _画面.注意絞り込み.Click += (_, _) => 絞り込み状態が変更された();
        _画面.検索絞り込み.TextChanged += (_, _) => 絞り込みを適用する();
        _画面.表示言語選択.SelectionChanged += (_, _) => 表示言語を変更する();
        _画面.テーマ選択.SelectionChanged += (_, _) => テーマを変更する();
        _画面.診断一覧.SelectionChanged += (_, _) => 選択した診断を更新する();
        _画面.診断一覧.DoubleTapped += async (_, _) => await 選択箇所を開く非同期();
        _画面.選択項目コピー.Click += async (_, _) => await 選択項目をコピーする非同期();
        _画面.全件コピー.Click += async (_, _) => await すべてコピーする非同期();
        _画面.ソースを開くボタン.Click += async (_, _) => await 選択箇所を開く非同期();
        _画面.JSON出力ボタン.Click += async (_, _) =>
            await 診断を出力する非同期(DiagnosticExportFormat.Json);
        _画面.SARIF出力ボタン.Click += async (_, _) =>
            await 診断を出力する非同期(DiagnosticExportFormat.Sarif);

        _画面.メインメニュー部品.対象ファイル項目.Click += async (_, _) =>
            await 解析対象ファイルを選択する非同期();
        _画面.メインメニュー部品.対象フォルダー項目.Click += async (_, _) =>
            await 解析対象フォルダーを選択する非同期();
        _画面.メインメニュー部品.設定項目.Click += async (_, _) =>
            await 設定ファイルを選択する非同期();
        _画面.メインメニュー部品.設定編集項目.Click += async (_, _) => await 設定を編集する非同期();
        _画面.メインメニュー部品.設定消去項目.Click += (_, _) => 設定消去();
        _画面.メインメニュー部品.JSON出力項目.Click += async (_, _) =>
            await 診断を出力する非同期(DiagnosticExportFormat.Json);
        _画面.メインメニュー部品.SARIF出力項目.Click += async (_, _) =>
            await 診断を出力する非同期(DiagnosticExportFormat.Sarif);
        _画面.メインメニュー部品.終了項目.Click += (_, _) => Close();

        _画面.メインメニュー部品.解析項目.Click += async (_, _) => await 解析する非同期();
        _画面.メインメニュー部品.キャンセル項目.Click += (_, _) => 解析をキャンセルする();
        _画面.メインメニュー部品.ソースを開く項目.Click += async (_, _) =>
            await 選択箇所を開く非同期();

        _画面.メインメニュー部品.検索項目.Click += (_, _) => 検索へフォーカスする();
        _画面.メインメニュー部品.危険絞り込み項目.Click += (_, _) =>
            メニューから絞り込みを切り替える(_画面.危険絞り込み);
        _画面.メインメニュー部品.警告絞り込み項目.Click += (_, _) =>
            メニューから絞り込みを切り替える(_画面.警告絞り込み);
        _画面.メインメニュー部品.注意絞り込み項目.Click += (_, _) =>
            メニューから絞り込みを切り替える(_画面.注意絞り込み);
        _画面.メインメニュー部品.日本語項目.Click += (_, _) => _画面.表示言語選択.SelectedIndex = 0;
        _画面.メインメニュー部品.英語項目.Click += (_, _) => _画面.表示言語選択.SelectedIndex = 1;
        _画面.メインメニュー部品.ダークテーマ項目.Click += (_, _) =>
            _画面.テーマ選択.SelectedIndex = 0;
        _画面.メインメニュー部品.ライトテーマ項目.Click += (_, _) =>
            _画面.テーマ選択.SelectedIndex = 1;

        _画面.対象パス欄.KeyDown += 編集欄キー押下時;
        _画面.設定パス欄.KeyDown += 編集欄キー押下時;
        _画面.検索絞り込み.KeyDown += 編集欄キー押下時;
        AddHandler(
            InputElement.KeyDownEvent,
            キー押下プレビュー時,
            RoutingStrategies.Tunnel,
            handledEventsToo: true
        );
        KeyDown += キー押下時;
    }

    private void 表示言語を変更する()
    {
        _表示言語 =
            _画面.表示言語選択.SelectedIndex == 1
                ? UserInterfaceLanguage.English
                : UserInterfaceLanguage.Japanese;
        Title = GUI文言.取得する(GUI文言キー.ウィンドウタイトル, _表示言語);
        _画面.表示言語を適用する(_表示言語);
        if (_前回解析結果 is not null)
        {
            _全診断行 = _前回解析結果
                .Diagnostics.Select(diagnostic => DiagnosticRow.From(diagnostic, _表示言語))
                .ToArray();
            絞り込みを適用する();
        }

        集計を更新する();
        選択した診断を更新する();
        言語別状態表示を更新する();
        操作の有効状態を更新する();
    }

    private void テーマを変更する()
    {
        if (Avalonia.Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant =
            _画面.テーマ選択.SelectedIndex == 1 ? ThemeVariant.Light : ThemeVariant.Dark;
        操作の有効状態を更新する();
    }

    private async Task 解析する非同期()
    {
        if (_解析中)
        {
            return;
        }

        var targetPath = _画面.対象パス欄.Text?.Trim();
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.対象必須, _表示言語);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _解析キャンセル = cancellation;
        処理中状態を設定する(true);

        try
        {
            var automationDelay = UI自動化設定.解析遅延ミリ秒;
            if (automationDelay > 0)
            {
                await Task.Delay(automationDelay, cancellation.Token);
            }

            var configurationPath = 省略可能なパスを正規化する(_画面.設定パス欄.Text);
            var result = await Task.Run(
                () =>
                    CheckerService.Analyze(
                        targetPath,
                        configurationPath,
                        cancellationToken: cancellation.Token
                    ),
                cancellation.Token
            );
            解析結果を表示する(result);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.解析キャンセル済み, _表示言語);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            エラーを表示する(LocalizedText.AnalysisFailed(_表示言語, exception.Message));
        }
        finally
        {
            if (ReferenceEquals(_解析キャンセル, cancellation))
            {
                _解析キャンセル = null;
            }

            処理中状態を設定する(false);
        }
    }

    private void 解析をキャンセルする()
    {
        if (_解析キャンセル is null || _解析キャンセル.IsCancellationRequested)
        {
            return;
        }

        _解析キャンセル.Cancel();
        _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.キャンセル中, _表示言語);
        操作の有効状態を更新する();
    }

    private void 解析結果を表示する(CheckerRunResult result)
    {
        _前回解析結果 = result;
        _全診断行 = result
            .Diagnostics.Select(diagnostic => DiagnosticRow.From(diagnostic, _表示言語))
            .ToArray();
        絞り込みを適用する();
        集計を更新する();
        _画面.状態表示.Text = GUI文言.書式化する(GUI文言キー.完了, _表示言語, _全診断行.Length);
        _画面.設定パス欄.Text = result.ConfigurationPath ?? _画面.設定パス欄.Text;
        操作の有効状態を更新する();
    }

    private void エラーを表示する(string message)
    {
        _前回解析結果 = null;
        _全診断行 = [];
        _画面.診断一覧.ItemsSource = Array.Empty<DiagnosticRow>();
        _画面.診断一覧.SelectedItem = null;
        _画面.詳細を消去する(_表示言語);
        集計を更新する();
        _画面.状態表示.Text = message;
        操作の有効状態を更新する();
    }

    private void 絞り込みを適用する()
    {
        var options = new DiagnosticFilterOptions(
            _画面.危険絞り込み.IsChecked == true,
            _画面.警告絞り込み.IsChecked == true,
            _画面.注意絞り込み.IsChecked == true,
            _画面.検索絞り込み.Text ?? string.Empty
        );
        _画面.診断一覧.ItemsSource = DiagnosticFilter.Apply(_全診断行, options);
        メニュー状態を同期する();
    }

    private void 集計を更新する()
    {
        _画面.危険件数表示.Text =
            $"{GUI文言.取得する(GUI文言キー.危険, _表示言語)}: {件数を数える(DesignDiagnosticSeverity.Danger)}";
        _画面.警告件数表示.Text =
            $"{GUI文言.取得する(GUI文言キー.警告, _表示言語)}: {件数を数える(DesignDiagnosticSeverity.Warning)}";
        _画面.注意件数表示.Text =
            $"{GUI文言.取得する(GUI文言キー.注意, _表示言語)}: {件数を数える(DesignDiagnosticSeverity.Attention)}";

        if (_前回解析結果 is null)
        {
            _画面.設定概要.Text = GUI文言.取得する(GUI文言キー.設定未読込, _表示言語);
            return;
        }

        var configuration = _前回解析結果.Configuration;
        var configName = _前回解析結果.ConfigurationPath is null
            ? GUI文言.取得する(GUI文言キー.設定既定値, _表示言語)
            : Path.GetFileName(_前回解析結果.ConfigurationPath);
        _画面.設定概要.Text = GUI文言.書式化する(
            GUI文言キー.設定概要文,
            _表示言語,
            configName,
            LocalizedText.SeverityName(_表示言語, _前回解析結果.FailureThreshold),
            configuration.DisabledRules.Count,
            configuration.IgnoredPaths.Count
        );
    }

    private int 件数を数える(DesignDiagnosticSeverity severity) =>
        _全診断行.Count(row => row.Severity == severity);

    private void 選択した診断を更新する()
    {
        if (_画面.診断一覧.SelectedItem is not DiagnosticRow row)
        {
            _画面.詳細を消去する(_表示言語);
            操作の有効状態を更新する();
            return;
        }

        _画面.詳細重要度.Text = GUI文言.書式化する(
            GUI文言キー.重要度詳細,
            _表示言語,
            LocalizedText.SeverityName(_表示言語, row.Severity)
        );
        _画面.詳細規則.Text = GUI文言.書式化する(
            GUI文言キー.規則詳細,
            _表示言語,
            $"{row.RuleId} — {row.RuleTitle}"
        );
        _画面.詳細シンボル.Text = GUI文言.書式化する(
            GUI文言キー.シンボル詳細,
            _表示言語,
            row.SymbolName ?? "-"
        );
        _画面.詳細位置.Text = GUI文言.書式化する(GUI文言キー.位置詳細, _表示言語, row.LocationText);
        _画面.詳細メッセージ.Text = row.Message;
        操作の有効状態を更新する();
    }

    private async Task 解析対象ファイルを選択する非同期()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = GUI文言.取得する(GUI文言キー.解析対象選択, _表示言語),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(GUI文言.取得する(GUI文言キー.CSharp対象種別, _表示言語))
                    {
                        Patterns = ["*.cs", "*.csproj", "*.sln", "*.slnx"],
                    },
                ],
            }
        );
        選択項目からパスを設定する(files.Count == 0 ? null : files[0], _画面.対象パス欄);
    }

    private async Task 解析対象フォルダーを選択する非同期()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = GUI文言.取得する(GUI文言キー.プロジェクトフォルダー選択, _表示言語),
                AllowMultiple = false,
            }
        );
        選択項目からパスを設定する(folders.Count == 0 ? null : folders[0], _画面.対象パス欄);
    }

    private async Task 設定ファイルを選択する非同期()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = GUI文言.取得する(GUI文言キー.チェッカー設定選択, _表示言語),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(GUI文言.取得する(GUI文言キー.JSON設定種別, _表示言語))
                    {
                        Patterns = ["*.json"],
                    },
                ],
            }
        );
        選択項目からパスを設定する(files.Count == 0 ? null : files[0], _画面.設定パス欄);
    }

    private async Task 設定を編集する非同期()
    {
        if (_解析中)
        {
            return;
        }

        try
        {
            var configurationPath = 設定編集パスを解決する();
            var initialJson =
                configurationPath is not null && File.Exists(configurationPath)
                    ? await File.ReadAllTextAsync(configurationPath)
                    : CheckerConfigurationJson.Serialize(
                        _前回解析結果?.Configuration ?? new CheckerConfiguration()
                    );

            var editor = new 設定編集画面(initialJson, _表示言語);
            var editedJson = await editor.ShowDialog<string?>(this);
            if (editedJson is null)
            {
                return;
            }

            _ = CheckerConfigurationJson.Parse(editedJson);
            configurationPath ??= await 設定保存先を選択する非同期();
            if (configurationPath is null)
            {
                return;
            }

            await File.WriteAllTextAsync(configurationPath, editedJson);
            _画面.設定パス欄.Text = configurationPath;
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.設定保存済み, _表示言語);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.操作失敗, _表示言語);
        }
    }

    private string? 設定編集パスを解決する()
    {
        var configuredPath = 省略可能なパスを正規化する(_画面.設定パス欄.Text);
        if (configuredPath is null)
        {
            return _前回解析結果?.ConfigurationPath;
        }

        return CheckerConfigurationPathResolver.ResolveExplicit(
            省略可能なパスを正規化する(_画面.対象パス欄.Text),
            configuredPath
        );
    }

    private async Task<string?> 設定保存先を選択する非同期()
    {
        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = GUI文言.取得する(GUI文言キー.チェッカー設定保存, _表示言語),
                SuggestedFileName = "oop-design-checker.json",
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType(GUI文言.取得する(GUI文言キー.JSON設定種別, _表示言語))
                    {
                        Patterns = ["*.json"],
                    },
                ],
            }
        );
        return file?.TryGetLocalPath();
    }

    private async Task 診断を出力する非同期(DiagnosticExportFormat format)
    {
        if (_前回解析結果 is null)
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.出力前解析, _表示言語);
            return;
        }

        var extension = format == DiagnosticExportFormat.Sarif ? "sarif" : "json";
        var description = format == DiagnosticExportFormat.Sarif ? "SARIF" : "JSON";
        var outputPath = UI自動化設定.出力先パス(format);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = GUI文言.書式化する(GUI文言キー.診断形式出力, _表示言語, description),
                    SuggestedFileName = $"oop-design-checker-results.{extension}",
                    DefaultExtension = extension,
                    FileTypeChoices =
                    [
                        new FilePickerFileType(
                            GUI文言.書式化する(GUI文言キー.診断ファイル種別, _表示言語, description)
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
                DiagnosticExportService.Export(_前回解析結果.Diagnostics, outputPath, format)
            );
            _画面.状態表示.Text = GUI文言.書式化する(
                GUI文言キー.診断出力済み,
                _表示言語,
                outputPath
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.操作失敗, _表示言語);
        }
    }

    private static void 選択項目からパスを設定する(IStorageItem? item, TextBox destination)
    {
        var path = item?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            destination.Text = path;
        }
    }

    private async Task 選択項目をコピーする非同期()
    {
        if (_画面.診断一覧.SelectedItem is DiagnosticRow row)
        {
            await テキストをコピーする非同期(row.CopyText);
        }
    }

    private async Task すべてコピーする非同期()
    {
        if (_全診断行.Length == 0)
        {
            return;
        }

        await テキストをコピーする非同期(
            string.Join(Environment.NewLine, _全診断行.Select(row => row.CopyText))
        );
    }

    private async Task テキストをコピーする非同期(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.クリップボード利用不可, _表示言語);
            return;
        }

        var copied = await クリップボード操作.テキスト設定を試す非同期(() =>
            clipboard.SetTextAsync(text)
        );
        _画面.状態表示.Text = GUI文言.取得する(
            copied ? GUI文言キー.クリップボードコピー済み : GUI文言キー.クリップボードコピー失敗,
            _表示言語
        );
    }

    private async Task 選択箇所を開く非同期()
    {
        if (_画面.診断一覧.SelectedItem is not DiagnosticRow row)
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
            await ソース位置をコピーする非同期(row.LocationText);
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.不正ソースパスコピー済み, _表示言語);
            return;
        }

        if (!File.Exists(sourcePath))
        {
            await ソース位置をコピーする非同期(row.LocationText);
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.ソース不在コピー済み, _表示言語);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(sourcePath) { UseShellExecute = true });
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.ソース表示済み, _表示言語);
        }
        catch (Exception exception)
            when (exception
                    is InvalidOperationException
                        or System.ComponentModel.Win32Exception
                        or IOException
                        or UnauthorizedAccessException
            )
        {
            await ソース位置をコピーする非同期(row.LocationText);
            _画面.状態表示.Text = GUI文言.取得する(
                GUI文言キー.ソースを開けない場合のコピー,
                _表示言語
            );
        }
    }

    private async Task ソース位置をコピーする非同期(string locationText)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            _ = await クリップボード操作.テキスト設定を試す非同期(() =>
                clipboard.SetTextAsync(locationText)
            );
        }
    }

    private void 設定消去()
    {
        _画面.設定パス欄.Text = string.Empty;
    }

    private void 絞り込み状態が変更された()
    {
        絞り込みを適用する();
        メニュー状態を同期する();
    }

    private void メニューから絞り込みを切り替える(CheckBox filter)
    {
        filter.IsChecked = filter.IsChecked != true;
        絞り込み状態が変更された();
    }

    private void 検索へフォーカスする()
    {
        _画面.検索絞り込み.Focus();
    }

    private void 編集欄キー押下時(object? sender, KeyEventArgs e)
    {
        if (!GUIコマンド.解析キャンセル.一致する(e))
        {
            return;
        }

        e.Handled = true;
        if (_解析中)
        {
            _画面.キャンセルボタン.Focus();
        }
        else
        {
            _画面.解析ボタン.Focus();
        }
    }

    private void キー押下プレビュー時(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            e.Handled = true;
            return;
        }

        if (GUIコマンド.解析キャンセル.一致する(e) && 最上位メニューが開いている())
        {
            最上位メニューを閉じる();
            e.Handled = true;
            return;
        }

        if (
            e.KeyModifiers == KeyModifiers.None
            && 最上位メニューが開いている()
            && メニューアクセスキーを処理する(e.Key)
        )
        {
            e.Handled = true;
            return;
        }

        if (Altアクセスキーを処理する(e))
        {
            e.Handled = true;
        }
    }

    private async void キー押下時(object? sender, KeyEventArgs e)
    {
        if (GUIコマンド.解析キャンセル.一致する(e) && _解析中)
        {
            e.Handled = true;
            解析をキャンセルする();
            return;
        }

        if (GUIコマンド.解析開始.一致する(e))
        {
            e.Handled = true;
            await 解析する非同期();
            return;
        }

        if (GUIコマンド.診断検索.一致する(e))
        {
            e.Handled = true;
            検索へフォーカスする();
            return;
        }

        if (GUIコマンド.選択項目コピー.一致する(e))
        {
            if (FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            e.Handled = true;
            await 選択項目をコピーする非同期();
        }
    }

    private bool メニューアクセスキーを処理する(Key key)
    {
        var menu = _画面.メインメニュー部品;

        if (menu.言語メニュー.IsSubMenuOpen)
        {
            if (メニューアクセスキーが一致する(GUIコマンド.日本語表示, key))
            {
                最上位メニューを閉じる();
                _画面.表示言語選択.SelectedIndex = 0;
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.英語表示, key))
            {
                最上位メニューを閉じる();
                _画面.表示言語選択.SelectedIndex = 1;
                return true;
            }
        }

        if (menu.テーマメニュー.IsSubMenuOpen)
        {
            if (メニューアクセスキーが一致する(GUIコマンド.ダークテーマ, key))
            {
                最上位メニューを閉じる();
                _画面.テーマ選択.SelectedIndex = 0;
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.ライトテーマ, key))
            {
                最上位メニューを閉じる();
                _画面.テーマ選択.SelectedIndex = 1;
                return true;
            }
        }

        if (menu.ファイルメニュー.IsSubMenuOpen)
        {
            if (メニューアクセスキーが一致する(GUIコマンド.対象ファイル選択, key))
            {
                最上位メニューを閉じる();
                _ = 解析対象ファイルを選択する非同期();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.対象フォルダー選択, key))
            {
                最上位メニューを閉じる();
                _ = 解析対象フォルダーを選択する非同期();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.設定選択, key))
            {
                最上位メニューを閉じる();
                _ = 設定ファイルを選択する非同期();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.設定編集, key))
            {
                最上位メニューを閉じる();
                _ = 設定を編集する非同期();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.設定消去, key))
            {
                最上位メニューを閉じる();
                設定消去();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.JSON出力, key))
            {
                最上位メニューを閉じる();
                _ = 診断を出力する非同期(DiagnosticExportFormat.Json);
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.SARIF出力, key))
            {
                最上位メニューを閉じる();
                _ = 診断を出力する非同期(DiagnosticExportFormat.Sarif);
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.終了, key))
            {
                Close();
                return true;
            }
        }

        if (menu.解析メニュー.IsSubMenuOpen)
        {
            if (メニューアクセスキーが一致する(GUIコマンド.解析開始, key))
            {
                最上位メニューを閉じる();
                _ = 解析する非同期();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.解析キャンセル, key))
            {
                最上位メニューを閉じる();
                解析をキャンセルする();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.ソースを開く, key))
            {
                最上位メニューを閉じる();
                _ = 選択箇所を開く非同期();
                return true;
            }
        }

        if (menu.表示メニュー.IsSubMenuOpen)
        {
            if (メニューアクセスキーが一致する(GUIコマンド.診断検索, key))
            {
                最上位メニューを閉じる();
                検索へフォーカスする();
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.危険絞り込み, key))
            {
                最上位メニューを閉じる();
                メニューから絞り込みを切り替える(_画面.危険絞り込み);
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.警告絞り込み, key))
            {
                最上位メニューを閉じる();
                メニューから絞り込みを切り替える(_画面.警告絞り込み);
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.注意絞り込み, key))
            {
                最上位メニューを閉じる();
                メニューから絞り込みを切り替える(_画面.注意絞り込み);
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.言語メニュー, key))
            {
                サブメニューを開く(menu.言語メニュー);
                return true;
            }

            if (メニューアクセスキーが一致する(GUIコマンド.テーマメニュー, key))
            {
                サブメニューを開く(menu.テーマメニュー);
                return true;
            }
        }

        return false;
    }

    private static void サブメニューを開く(MenuItem menu)
    {
        menu.Open();
    }

    private bool Altアクセスキーを処理する(KeyEventArgs e)
    {
        if (最上位アクセスキーが一致する(GUIコマンド.ファイルメニュー, e))
        {
            最上位メニューを開く(_画面.メインメニュー部品.ファイルメニュー);
            return true;
        }

        if (最上位アクセスキーが一致する(GUIコマンド.解析メニュー, e))
        {
            最上位メニューを開く(_画面.メインメニュー部品.解析メニュー);
            return true;
        }

        if (最上位アクセスキーが一致する(GUIコマンド.表示メニュー, e))
        {
            最上位メニューを開く(_画面.メインメニュー部品.表示メニュー);
            return true;
        }

        if (GUIコマンド.対象欄フォーカス.一致する(e))
        {
            _画面.対象パス欄.Focus();
            return true;
        }

        if (GUIコマンド.設定欄フォーカス.一致する(e))
        {
            _画面.設定パス欄.Focus();
            return true;
        }

        return false;
    }

    private static bool メニューアクセスキーが一致する(GUIコマンド定義 command, Key key) =>
        command.メニューアクセスキー == key;

    private static bool 最上位アクセスキーが一致する(GUIコマンド定義 command, KeyEventArgs e) =>
        e.KeyModifiers == KeyModifiers.Alt
        && command.メニュー経路.Count == 1
        && command.メニュー経路[0] == e.Key;

    private bool 最上位メニューが開いている() =>
        _画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen
        || _画面.メインメニュー部品.解析メニュー.IsSubMenuOpen
        || _画面.メインメニュー部品.表示メニュー.IsSubMenuOpen;

    private void 最上位メニューを閉じる()
    {
        _画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen = false;
        _画面.メインメニュー部品.解析メニュー.IsSubMenuOpen = false;
        _画面.メインメニュー部品.表示メニュー.IsSubMenuOpen = false;
    }

    private void 最上位メニューを開く(MenuItem menu)
    {
        最上位メニューを閉じる();
        menu.Open();
    }

    private void 処理中状態を設定する(bool isBusy)
    {
        _解析中 = isBusy;
        _画面.進捗表示.IsVisible = isBusy;
        操作の有効状態を更新する();
        if (isBusy)
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.解析中, _表示言語);
        }
    }

    private void 操作の有効状態を更新する()
    {
        var canEdit = !_解析中;
        var canCancel = _解析中 && _解析キャンセル is { IsCancellationRequested: false };
        var hasResult = _前回解析結果 is not null;
        var hasSelection = _画面.診断一覧.SelectedItem is DiagnosticRow;

        _画面.解析ボタン.IsEnabled = canEdit;
        _画面.キャンセルボタン.IsEnabled = canCancel;
        _画面.対象ファイルボタン.IsEnabled = canEdit;
        _画面.対象フォルダーボタン.IsEnabled = canEdit;
        _画面.設定選択ボタン.IsEnabled = canEdit;
        _画面.設定編集ボタン.IsEnabled = canEdit;
        _画面.設定消去ボタン.IsEnabled = canEdit;
        _画面.JSON出力ボタン.IsEnabled = canEdit && hasResult;
        _画面.SARIF出力ボタン.IsEnabled = canEdit && hasResult;
        _画面.ソースを開くボタン.IsEnabled = canEdit && hasSelection;
        _画面.選択項目コピー.IsEnabled = hasSelection;
        _画面.全件コピー.IsEnabled = hasResult;
        _画面.表示言語選択.IsEnabled = canEdit;
        _画面.テーマ選択.IsEnabled = canEdit;

        var menu = _画面.メインメニュー部品;
        menu.対象ファイル項目.IsEnabled = canEdit;
        menu.対象フォルダー項目.IsEnabled = canEdit;
        menu.設定項目.IsEnabled = canEdit;
        menu.設定編集項目.IsEnabled = canEdit;
        menu.設定消去項目.IsEnabled = canEdit;
        menu.JSON出力項目.IsEnabled = canEdit && hasResult;
        menu.SARIF出力項目.IsEnabled = canEdit && hasResult;
        menu.解析項目.IsEnabled = canEdit;
        menu.キャンセル項目.IsEnabled = canCancel;
        menu.ソースを開く項目.IsEnabled = canEdit && hasSelection;
        menu.言語メニュー.IsEnabled = canEdit;
        menu.テーマメニュー.IsEnabled = canEdit;

        メニュー状態を同期する();
    }

    private void メニュー状態を同期する()
    {
        var menu = _画面.メインメニュー部品;
        menu.危険絞り込み項目.IsChecked = _画面.危険絞り込み.IsChecked == true;
        menu.警告絞り込み項目.IsChecked = _画面.警告絞り込み.IsChecked == true;
        menu.注意絞り込み項目.IsChecked = _画面.注意絞り込み.IsChecked == true;
        menu.日本語項目.IsChecked = _画面.表示言語選択.SelectedIndex != 1;
        menu.英語項目.IsChecked = _画面.表示言語選択.SelectedIndex == 1;
        menu.ダークテーマ項目.IsChecked = _画面.テーマ選択.SelectedIndex != 1;
        menu.ライトテーマ項目.IsChecked = _画面.テーマ選択.SelectedIndex == 1;
    }

    private void 言語別状態表示を更新する()
    {
        if (_解析中)
        {
            _画面.状態表示.Text = GUI文言.取得する(GUI文言キー.解析中, _表示言語);
            return;
        }

        _画面.状態表示.Text = _前回解析結果 is null
            ? GUI文言.取得する(GUI文言キー.準備完了, _表示言語)
            : GUI文言.書式化する(GUI文言キー.完了, _表示言語, _全診断行.Length);
    }

    private static string? 省略可能なパスを正規化する(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();
}
