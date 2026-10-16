using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal sealed class メイン画面 : Grid
{
    internal メインメニュー メインメニュー部品 { get; } = new();
    internal TextBox 対象パス欄 { get; } = new();
    internal TextBox 設定パス欄 { get; } = new();
    internal Button 対象ファイルボタン { get; } = new();
    internal Button 対象フォルダーボタン { get; } = new();
    internal Button 設定選択ボタン { get; } = new();
    internal Button 設定編集ボタン { get; } = new();
    internal Button 設定消去ボタン { get; } = new();
    internal Button 解析ボタン { get; } = new();
    internal Button キャンセルボタン { get; } = new();
    internal ProgressBar 進捗表示 { get; } = new();
    internal TextBlock 状態表示 { get; } = new();
    internal TextBlock 危険件数表示 { get; } = new();
    internal TextBlock 警告件数表示 { get; } = new();
    internal TextBlock 注意件数表示 { get; } = new();
    internal CheckBox 危険絞り込み { get; } = new();
    internal CheckBox 警告絞り込み { get; } = new();
    internal CheckBox 注意絞り込み { get; } = new();
    internal TextBox 検索絞り込み { get; } = new();
    internal TextBlock ショートカット案内 { get; } = new();
    internal ComboBox 表示言語選択 { get; } = new();
    internal ComboBox テーマ選択 { get; } = new();
    internal DataGrid 診断一覧 { get; } = new();
    internal TextBlock 詳細重要度 { get; } = new();
    internal TextBlock 詳細規則 { get; } = new();
    internal TextBlock 詳細シンボル { get; } = new();
    internal TextBlock 詳細位置 { get; } = new();
    internal TextBlock 詳細メッセージ { get; } = new();
    internal TextBlock 設定概要 { get; } = new();
    internal Button 選択項目コピー { get; } = new();
    internal Button 全件コピー { get; } = new();
    internal Button ソースを開くボタン { get; } = new();
    internal Button JSON出力ボタン { get; } = new();
    internal Button SARIF出力ボタン { get; } = new();

    private readonly Label _対象ラベル = new();
    private readonly Label _設定ラベル = new();
    private readonly TextBlock _表示ラベル = new();
    private readonly TextBlock _言語ラベル = new();
    private readonly TextBlock _テーマラベル = new();
    private readonly TextBlock _詳細見出し = new();

    public メイン画面()
    {
        Margin = new Thickness(16);
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowSpacing = 10;

        コントロールを設定する();
        自動化設定を適用する();
        欄を追加する(メインメニュー部品, 0);
        欄を追加する(対象欄を構築する(), 1);
        欄を追加する(絞り込み欄を構築する(), 2);
        欄を追加する(集計欄を構築する(), 3);
        欄を追加する(診断一覧欄を構築する(), 4);
        欄を追加する(状態欄を構築する(), 5);
        表示言語を適用する(UserInterfaceLanguage.Japanese);
    }

    internal void 表示言語を適用する(UserInterfaceLanguage language)
    {
        メインメニュー部品.表示言語を適用する(language);
        _対象ラベル.Content =
            $"{GUI文言.取得する(GUI文言キー.対象ラベル, language)} (_{GUIショートカット.キー名を整形する(GUIコマンド.対象欄フォーカス.主ショートカット!.Value.Key)})  {GUIコマンド.対象欄フォーカス.主操作の案内}:";
        _設定ラベル.Content =
            $"{GUI文言.取得する(GUI文言キー.設定ラベル, language)} (_{GUIショートカット.キー名を整形する(GUIコマンド.設定欄フォーカス.主ショートカット!.Value.Key)})  {GUIコマンド.設定欄フォーカス.主操作の案内}:";
        対象パス欄.PlaceholderText = GUI文言.取得する(GUI文言キー.対象入力案内, language);
        設定パス欄.PlaceholderText = GUI文言.取得する(GUI文言キー.設定入力案内, language);

        対象ファイルボタン.Content = GUI文言.取得する(GUI文言キー.ファイルボタン, language);
        対象フォルダーボタン.Content = GUI文言.取得する(GUI文言キー.フォルダーボタン, language);
        設定選択ボタン.Content = GUI文言.取得する(GUI文言キー.設定ボタン文言, language);
        設定編集ボタン.Content = GUI文言.取得する(GUI文言キー.編集ボタン, language);
        設定消去ボタン.Content = GUI文言.取得する(GUI文言キー.消去ボタン, language);
        解析ボタン.Content = GUI文言.取得する(GUI文言キー.解析ボタン, language);
        キャンセルボタン.Content = GUI文言.取得する(GUI文言キー.キャンセルボタン, language);

        _表示ラベル.Text = GUI文言.取得する(GUI文言キー.表示ラベル, language);
        _言語ラベル.Text = GUI文言.取得する(GUI文言キー.言語ラベル, language);
        _テーマラベル.Text = GUI文言.取得する(GUI文言キー.テーマラベル, language);
        var selectedTheme = Math.Max(テーマ選択.SelectedIndex, 0);
        テーマ選択.ItemsSource = new[]
        {
            GUI文言.取得する(GUI文言キー.ダークテーマ, language),
            GUI文言.取得する(GUI文言キー.ライトテーマ, language),
        };
        テーマ選択.SelectedIndex = selectedTheme;
        危険絞り込み.Content = GUI文言.取得する(GUI文言キー.危険, language);
        警告絞り込み.Content = GUI文言.取得する(GUI文言キー.警告, language);
        注意絞り込み.Content = GUI文言.取得する(GUI文言キー.注意, language);
        検索絞り込み.PlaceholderText = GUI文言.取得する(GUI文言キー.検索入力案内, language);
        ショートカット案内.Text =
            $"{GUI文言.取得する(GUI文言キー.解析ボタン, language)}: {GUIコマンド.解析開始.主操作の案内}  ·  "
            + $"{GUI文言.取得する(GUI文言キー.キャンセルボタン, language)}: {GUIコマンド.解析キャンセル.主操作の案内}  ·  "
            + $"{GUI文言.取得する(GUI文言キー.検索メニュー, language)}: {GUIコマンド.診断検索.主操作の案内}";

        選択項目コピー.Content = GUI文言.取得する(GUI文言キー.選択項目コピー, language);
        全件コピー.Content = GUI文言.取得する(GUI文言キー.全件コピー, language);
        ソースを開くボタン.Content = GUI文言.取得する(GUI文言キー.ソースを開くボタン, language);
        JSON出力ボタン.Content = GUI文言.取得する(GUI文言キー.JSON出力ボタン, language);
        SARIF出力ボタン.Content = GUI文言.取得する(GUI文言キー.SARIF出力ボタン, language);
        _詳細見出し.Text = GUI文言.取得する(GUI文言キー.診断詳細, language);

        診断一覧.Columns[0].Header = GUI文言.取得する(GUI文言キー.重要度列, language);
        診断一覧.Columns[1].Header = GUI文言.取得する(GUI文言キー.規則列, language);
        診断一覧.Columns[2].Header = GUI文言.取得する(GUI文言キー.ファイル列, language);
        診断一覧.Columns[3].Header = GUI文言.取得する(GUI文言キー.行列, language);
        診断一覧.Columns[4].Header = GUI文言.取得する(GUI文言キー.列列, language);
        診断一覧.Columns[5].Header = GUI文言.取得する(GUI文言キー.メッセージ列, language);

        コマンドヘルプを適用する(language);
        詳細を消去する(language);
    }

    private void 自動化設定を適用する()
    {
        var controls = new (StyledElement Element, string 識別子)[]
        {
            (_対象ラベル, "TargetPathLabel"),
            (_設定ラベル, "ConfigurationPathLabel"),
            (対象パス欄, "TargetPath"),
            (設定パス欄, "ConfigurationPath"),
            (対象ファイルボタン, "TargetFileButton"),
            (対象フォルダーボタン, "TargetFolderButton"),
            (設定選択ボタン, "ConfigurationButton"),
            (設定編集ボタン, "EditConfigurationButton"),
            (設定消去ボタン, "ClearConfigurationButton"),
            (解析ボタン, "AnalyzeButton"),
            (キャンセルボタン, "CancelButton"),
            (進捗表示, "AnalysisProgress"),
            (状態表示, "Status"),
            (危険件数表示, "DangerCount"),
            (警告件数表示, "WarningCount"),
            (注意件数表示, "AttentionCount"),
            (危険絞り込み, "DangerFilter"),
            (警告絞り込み, "WarningFilter"),
            (注意絞り込み, "AttentionFilter"),
            (検索絞り込み, "SearchFilter"),
            (ショートカット案内, "ShortcutHints"),
            (表示言語選択, "LanguageSelector"),
            (テーマ選択, "ThemeSelector"),
            (診断一覧, "DiagnosticsGrid"),
            (詳細重要度, "DetailSeverity"),
            (詳細規則, "DetailRule"),
            (詳細シンボル, "DetailSymbol"),
            (詳細位置, "DetailLocation"),
            (詳細メッセージ, "DetailMessage"),
            (設定概要, "ConfigurationSummary"),
            (選択項目コピー, "CopySelectedButton"),
            (全件コピー, "CopyAllButton"),
            (ソースを開くボタン, "OpenSourceButton"),
            (JSON出力ボタン, "ExportJsonButton"),
            (SARIF出力ボタン, "ExportSarifButton"),
        };

        foreach (var (element, id) in controls)
        {
            AutomationProperties.SetAutomationId(element, id);
        }
    }

    private void コントロールを設定する()
    {
        対象パス欄.Text = Directory.GetCurrentDirectory();

        _対象ラベル.Target = 対象パス欄;
        _対象ラベル.VerticalAlignment = VerticalAlignment.Center;
        _設定ラベル.Target = 設定パス欄;
        _設定ラベル.VerticalAlignment = VerticalAlignment.Center;
        ショートカット案内.FontSize = 11;
        ショートカット案内.Opacity = 0.75;
        ショートカット案内.TextWrapping = TextWrapping.Wrap;

        タブ順を設定する();

        キャンセルボタン.IsEnabled = false;
        危険絞り込み.IsChecked = true;
        警告絞り込み.IsChecked = true;
        注意絞り込み.IsChecked = true;

        表示言語選択.ItemsSource = new[] { "日本語", "English" };
        表示言語選択.SelectedIndex = 0;
        表示言語選択.MinWidth = 100;

        テーマ選択.ItemsSource = new[] { "ダーク", "ライト" };
        テーマ選択.SelectedIndex = 0;
        テーマ選択.MinWidth = 100;

        進捗表示.IsIndeterminate = true;
        進捗表示.IsVisible = false;
        詳細メッセージ.TextWrapping = TextWrapping.Wrap;
        詳細位置.TextWrapping = TextWrapping.Wrap;
        設定概要.TextWrapping = TextWrapping.Wrap;

        診断一覧を設定する();
    }

    private void コマンドヘルプを適用する(UserInterfaceLanguage language)
    {
        コマンドヘルプを適用する(_対象ラベル, GUIコマンド.対象欄フォーカス, language);
        コマンドヘルプを適用する(対象パス欄, GUIコマンド.対象欄フォーカス, language);
        コマンドヘルプを適用する(_設定ラベル, GUIコマンド.設定欄フォーカス, language);
        コマンドヘルプを適用する(設定パス欄, GUIコマンド.設定欄フォーカス, language);

        コマンドヘルプを適用する(対象ファイルボタン, GUIコマンド.対象ファイル選択, language);
        コマンドヘルプを適用する(対象フォルダーボタン, GUIコマンド.対象フォルダー選択, language);
        コマンドヘルプを適用する(設定選択ボタン, GUIコマンド.設定選択, language);
        コマンドヘルプを適用する(設定編集ボタン, GUIコマンド.設定編集, language);
        コマンドヘルプを適用する(設定消去ボタン, GUIコマンド.設定消去, language);
        コマンドヘルプを適用する(解析ボタン, GUIコマンド.解析開始, language);
        コマンドヘルプを適用する(キャンセルボタン, GUIコマンド.解析キャンセル, language);
        コマンドヘルプを適用する(検索絞り込み, GUIコマンド.診断検索, language);

        コマンドヘルプを適用する(危険絞り込み, GUIコマンド.危険絞り込み, language);
        コマンドヘルプを適用する(警告絞り込み, GUIコマンド.警告絞り込み, language);
        コマンドヘルプを適用する(注意絞り込み, GUIコマンド.注意絞り込み, language);
        コマンドヘルプを適用する(表示言語選択, GUIコマンド.言語メニュー, language);
        コマンドヘルプを適用する(テーマ選択, GUIコマンド.テーマメニュー, language);

        コマンドヘルプを適用する(選択項目コピー, GUIコマンド.選択項目コピー, language);
        コマンドヘルプを適用する(全件コピー, GUIコマンド.全件コピー, language);
        コマンドヘルプを適用する(ソースを開くボタン, GUIコマンド.ソースを開く, language);
        コマンドヘルプを適用する(JSON出力ボタン, GUIコマンド.JSON出力, language);
        コマンドヘルプを適用する(SARIF出力ボタン, GUIコマンド.SARIF出力, language);

        AutomationProperties.SetHelpText(
            ショートカット案内,
            language == UserInterfaceLanguage.Japanese
                ? "主要shortcut。詳細は各controlのtooltipまたはメニューで確認できます。"
                : "Primary shortcuts. More shortcuts are available in control tooltips and menus."
        );
    }

    private static void コマンドヘルプを適用する(
        Control control,
        GUIコマンド定義 command,
        UserInterfaceLanguage language
    ) => command.ヘルプを適用する(control, language);

    private void タブ順を設定する()
    {
        対象パス欄.TabIndex = 0;
        対象ファイルボタン.TabIndex = 1;
        対象フォルダーボタン.TabIndex = 2;
        設定パス欄.TabIndex = 3;
        設定選択ボタン.TabIndex = 4;
        設定編集ボタン.TabIndex = 5;
        設定消去ボタン.TabIndex = 6;
        危険絞り込み.TabIndex = 7;
        警告絞り込み.TabIndex = 8;
        注意絞り込み.TabIndex = 9;
        検索絞り込み.TabIndex = 10;
        解析ボタン.TabIndex = 11;
        キャンセルボタン.TabIndex = 12;
        表示言語選択.TabIndex = 13;
        テーマ選択.TabIndex = 14;
        診断一覧.TabIndex = 15;
        選択項目コピー.TabIndex = 16;
        ソースを開くボタン.TabIndex = 17;
        全件コピー.TabIndex = 18;
        JSON出力ボタン.TabIndex = 19;
        SARIF出力ボタン.TabIndex = 20;
    }

    private void 診断一覧を設定する()
    {
        診断一覧.AutoGenerateColumns = false;
        診断一覧.IsReadOnly = true;
        診断一覧.CanUserReorderColumns = true;
        診断一覧.CanUserResizeColumns = true;
        診断一覧.CanUserSortColumns = true;
        診断一覧.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        診断一覧.SelectionMode = DataGridSelectionMode.Single;
        診断一覧.MinHeight = 260;

        診断一覧.Columns.Add(文字列列を作成する(string.Empty, nameof(DiagnosticRow.SeverityText)));
        診断一覧.Columns.Add(文字列列を作成する(string.Empty, nameof(DiagnosticRow.RuleId)));
        診断一覧.Columns.Add(文字列列を作成する(string.Empty, nameof(DiagnosticRow.FileName)));
        診断一覧.Columns.Add(文字列列を作成する(string.Empty, nameof(DiagnosticRow.Line)));
        診断一覧.Columns.Add(文字列列を作成する(string.Empty, nameof(DiagnosticRow.Column)));
        診断一覧.Columns.Add(文字列列を作成する(string.Empty, nameof(DiagnosticRow.Message)));
    }

    private Grid 対象欄を構築する()
    {
        var section = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        section.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        一覧へ追加する(section, _対象ラベル, 0, 0);
        一覧へ追加する(section, 対象パス欄, 0, 1);
        一覧へ追加する(section, 対象ファイルボタン, 0, 2);
        一覧へ追加する(section, 対象フォルダーボタン, 0, 3);
        一覧へ追加する(section, _設定ラベル, 1, 0);
        一覧へ追加する(section, 設定パス欄, 1, 1);
        一覧へ追加する(section, 設定選択ボタン, 1, 2);
        一覧へ追加する(section, 設定編集ボタン, 1, 3);
        一覧へ追加する(section, 設定消去ボタン, 1, 4);
        return section;
    }

    private Grid 絞り込み欄を構築する()
    {
        var grid = new Grid { RowSpacing = 8 };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _表示ラベル.VerticalAlignment = VerticalAlignment.Center;
        filters.Children.Add(_表示ラベル);
        filters.Children.Add(危険絞り込み);
        filters.Children.Add(警告絞り込み);
        filters.Children.Add(注意絞り込み);
        filters.Children.Add(検索絞り込み);
        filters.Children.Add(解析ボタン);
        filters.Children.Add(キャンセルボタン);

        var preferences = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _言語ラベル.VerticalAlignment = VerticalAlignment.Center;
        _テーマラベル.VerticalAlignment = VerticalAlignment.Center;
        preferences.Children.Add(_言語ラベル);
        preferences.Children.Add(表示言語選択);
        preferences.Children.Add(_テーマラベル);
        preferences.Children.Add(テーマ選択);

        一覧へ追加する(grid, filters, 0, 0);
        一覧へ追加する(grid, preferences, 1, 0);
        一覧へ追加する(grid, ショートカット案内, 2, 0);
        return grid;
    }

    private StackPanel 集計欄を構築する()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        panel.Children.Add(危険件数表示);
        panel.Children.Add(警告件数表示);
        panel.Children.Add(注意件数表示);
        panel.Children.Add(設定概要);
        return panel;
    }

    private Grid 診断一覧欄を構築する()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));

        一覧へ追加する(grid, 診断一覧, 0, 0);
        一覧へ追加する(grid, 詳細欄を構築する(), 0, 1);
        return grid;
    }

    private Border 詳細欄を構築する()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        _詳細見出し.FontSize = 18;
        panel.Children.Add(_詳細見出し);
        panel.Children.Add(詳細重要度);
        panel.Children.Add(詳細規則);
        panel.Children.Add(詳細シンボル);
        panel.Children.Add(詳細位置);
        panel.Children.Add(詳細メッセージ);

        var diagnosticActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        diagnosticActions.Children.Add(選択項目コピー);
        diagnosticActions.Children.Add(ソースを開くボタン);
        panel.Children.Add(diagnosticActions);

        var resultActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        resultActions.Children.Add(全件コピー);
        resultActions.Children.Add(JSON出力ボタン);
        resultActions.Children.Add(SARIF出力ボタン);
        panel.Children.Add(resultActions);

        return new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = panel,
        };
    }

    private Grid 状態欄を構築する()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
        一覧へ追加する(grid, 状態表示, 0, 0);
        一覧へ追加する(grid, 進捗表示, 0, 1);
        return grid;
    }

    internal void 詳細を消去する(UserInterfaceLanguage language)
    {
        詳細重要度.Text = GUI文言.書式化する(GUI文言キー.重要度詳細, language, "-");
        詳細規則.Text = GUI文言.書式化する(GUI文言キー.規則詳細, language, "-");
        詳細シンボル.Text = GUI文言.書式化する(GUI文言キー.シンボル詳細, language, "-");
        詳細位置.Text = GUI文言.書式化する(GUI文言キー.位置詳細, language, "-");
        詳細メッセージ.Text = GUI文言.取得する(GUI文言キー.診断選択案内, language);
    }

    private void 欄を追加する(Control control, int row)
    {
        Grid.SetRow(control, row);
        Children.Add(control);
    }

    private static DataGridTextColumn 文字列列を作成する(string header, string propertyName) =>
        new() { Header = header, Binding = new Binding(propertyName) };

    private static void 一覧へ追加する(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }
}
