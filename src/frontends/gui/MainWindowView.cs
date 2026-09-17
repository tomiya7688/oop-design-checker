using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal sealed class MainWindowView : Grid
{
    internal TextBox TargetPath { get; } = new();
    internal TextBox ConfigurationPath { get; } = new();
    internal Button TargetFileButton { get; } = new();
    internal Button TargetFolderButton { get; } = new();
    internal Button ConfigurationButton { get; } = new();
    internal Button EditConfigurationButton { get; } = new();
    internal Button ClearConfigurationButton { get; } = new();
    internal Button AnalyzeButton { get; } = new();
    internal Button CancelButton { get; } = new();
    internal ComboBox LanguageSelector { get; } = new();
    internal TextBlock LanguageLabel { get; } = new();
    internal ProgressBar Progress { get; } = new();
    internal TextBlock Status { get; } = new();
    internal TextBlock DangerCount { get; } = new();
    internal TextBlock WarningCount { get; } = new();
    internal TextBlock AttentionCount { get; } = new();
    internal CheckBox DangerFilter { get; } = new();
    internal CheckBox WarningFilter { get; } = new();
    internal CheckBox AttentionFilter { get; } = new();
    internal TextBox SearchFilter { get; } = new();
    internal DataGrid DiagnosticsGrid { get; } = new();
    internal TextBlock DetailSeverity { get; } = new();
    internal TextBlock DetailRule { get; } = new();
    internal TextBlock DetailSymbol { get; } = new();
    internal TextBlock DetailLocation { get; } = new();
    internal TextBlock DetailMessage { get; } = new();
    internal TextBlock ConfigurationSummary { get; } = new();
    internal Button CopySelectedButton { get; } = new();
    internal Button CopyAllButton { get; } = new();
    internal Button OpenSourceButton { get; } = new();
    internal Button ExportJsonButton { get; } = new();
    internal Button ExportSarifButton { get; } = new();

    public MainWindowView()
    {
        Margin = new Thickness(16);
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowSpacing = 10;

        ConfigureControls();
        AddSection(BuildTargetSection(), 0);
        AddSection(BuildFilterSection(), 1);
        AddSection(BuildSummarySection(), 2);
        AddSection(BuildDiagnosticsSection(), 3);
        AddSection(BuildStatusSection(), 4);
    }

    internal void ApplyLanguage(UserInterfaceLanguage language)
    {
        TargetPath.PlaceholderText = Text(
            language,
            "project、solution、C# file、またはproject directory",
            "Project, solution, C# file, or project directory"
        );
        ConfigurationPath.PlaceholderText = Text(
            language,
            "任意: oop-design-checker.json",
            "Optional oop-design-checker.json"
        );

        TargetFileButton.Content = Text(language, "ファイル...", "File...");
        TargetFolderButton.Content = Text(language, "フォルダー...", "Folder...");
        ConfigurationButton.Content = Text(language, "設定...", "Config...");
        EditConfigurationButton.Content = Text(language, "編集...", "Edit...");
        ClearConfigurationButton.Content = Text(language, "クリア", "Clear");
        AnalyzeButton.Content = Text(language, "解析", "Analyze");
        CancelButton.Content = Text(language, "キャンセル", "Cancel");
        LanguageLabel.Text = Text(language, "言語:", "Language:");

        DangerFilter.Content = Text(language, "危険", "Danger");
        WarningFilter.Content = Text(language, "警告", "Warning");
        AttentionFilter.Content = Text(language, "注意", "Attention");
        SearchFilter.PlaceholderText = Text(
            language,
            "rule、file、symbol、messageで絞り込み",
            "Filter by rule, file, symbol, or message"
        );

        CopySelectedButton.Content = Text(language, "選択をコピー", "Copy selected");
        CopyAllButton.Content = Text(language, "すべてコピー", "Copy all");
        OpenSourceButton.Content = Text(language, "sourceを開く", "Open source");
        ExportJsonButton.Content = Text(language, "JSON出力...", "Export JSON...");
        ExportSarifButton.Content = Text(language, "SARIF出力...", "Export SARIF...");

        DiagnosticsGrid.Columns[0].Header = Text(language, "重大度", "Severity");
        DiagnosticsGrid.Columns[1].Header = Text(language, "ルール", "Rule");
        DiagnosticsGrid.Columns[2].Header = Text(language, "ファイル", "File");
        DiagnosticsGrid.Columns[3].Header = Text(language, "行", "Line");
        DiagnosticsGrid.Columns[4].Header = Text(language, "列", "Column");
        DiagnosticsGrid.Columns[5].Header = Text(language, "メッセージ", "Message");
    }

    internal void ClearDetails(UserInterfaceLanguage language)
    {
        DetailSeverity.Text = Text(language, "重大度: -", "Severity: -");
        DetailRule.Text = Text(language, "ルール: -", "Rule: -");
        DetailSymbol.Text = Text(language, "シンボル: -", "Symbol: -");
        DetailLocation.Text = Text(language, "場所: -", "Location: -");
        DetailMessage.Text = Text(
            language,
            "診断を選択すると詳細を表示します。",
            "Select a diagnostic to see its full message."
        );
    }

    private void ConfigureControls()
    {
        TargetPath.Text = Directory.GetCurrentDirectory();
        CancelButton.IsEnabled = false;

        DangerFilter.IsChecked = true;
        WarningFilter.IsChecked = true;
        AttentionFilter.IsChecked = true;

        LanguageSelector.ItemsSource = new[] { "日本語", "English" };
        LanguageSelector.SelectedIndex = 0;
        LanguageSelector.MinWidth = 100;

        Progress.IsIndeterminate = true;
        Progress.IsVisible = false;
        DetailMessage.TextWrapping = TextWrapping.Wrap;
        DetailLocation.TextWrapping = TextWrapping.Wrap;
        ConfigurationSummary.TextWrapping = TextWrapping.Wrap;

        ConfigureDiagnosticsGrid();
        ApplyLanguage(UserInterfaceLanguage.Japanese);
        Status.Text = "準備完了";
        ClearDetails(UserInterfaceLanguage.Japanese);
    }

    private void ConfigureDiagnosticsGrid()
    {
        DiagnosticsGrid.AutoGenerateColumns = false;
        DiagnosticsGrid.IsReadOnly = true;
        DiagnosticsGrid.CanUserReorderColumns = true;
        DiagnosticsGrid.CanUserResizeColumns = true;
        DiagnosticsGrid.CanUserSortColumns = true;
        DiagnosticsGrid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        DiagnosticsGrid.SelectionMode = DataGridSelectionMode.Single;
        DiagnosticsGrid.MinHeight = 260;

        DiagnosticsGrid.Columns.Add(CreateTextColumn(string.Empty, nameof(DiagnosticRow.SeverityText)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn(string.Empty, nameof(DiagnosticRow.RuleId)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn(string.Empty, nameof(DiagnosticRow.FileName)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn(string.Empty, nameof(DiagnosticRow.Line)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn(string.Empty, nameof(DiagnosticRow.Column)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn(string.Empty, nameof(DiagnosticRow.Message)));
    }

    private Grid BuildTargetSection()
    {
        var section = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        section.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        section.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddToGrid(section, TargetPath, 0, 0);
        AddToGrid(section, TargetFileButton, 0, 1);
        AddToGrid(section, TargetFolderButton, 0, 2);
        AddToGrid(section, ConfigurationPath, 1, 0);
        AddToGrid(section, ConfigurationButton, 1, 1);
        AddToGrid(section, EditConfigurationButton, 1, 2);
        AddToGrid(section, ClearConfigurationButton, 1, 3);
        return section;
    }

    private StackPanel BuildFilterSection()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(DangerFilter);
        panel.Children.Add(WarningFilter);
        panel.Children.Add(AttentionFilter);
        panel.Children.Add(SearchFilter);
        panel.Children.Add(LanguageLabel);
        panel.Children.Add(LanguageSelector);
        panel.Children.Add(AnalyzeButton);
        panel.Children.Add(CancelButton);
        return panel;
    }

    private StackPanel BuildSummarySection()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        panel.Children.Add(DangerCount);
        panel.Children.Add(WarningCount);
        panel.Children.Add(AttentionCount);
        panel.Children.Add(ConfigurationSummary);
        return panel;
    }

    private Grid BuildDiagnosticsSection()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));

        AddToGrid(grid, DiagnosticsGrid, 0, 0);
        AddToGrid(grid, BuildDetailPane(), 0, 1);
        return grid;
    }

    private Border BuildDetailPane()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        panel.Children.Add(DetailSeverity);
        panel.Children.Add(DetailRule);
        panel.Children.Add(DetailSymbol);
        panel.Children.Add(DetailLocation);
        panel.Children.Add(DetailMessage);

        var diagnosticActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        diagnosticActions.Children.Add(CopySelectedButton);
        diagnosticActions.Children.Add(OpenSourceButton);
        panel.Children.Add(diagnosticActions);

        var resultActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        resultActions.Children.Add(CopyAllButton);
        resultActions.Children.Add(ExportJsonButton);
        resultActions.Children.Add(ExportSarifButton);
        panel.Children.Add(resultActions);

        return new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = panel,
        };
    }

    private Grid BuildStatusSection()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
        AddToGrid(grid, Status, 0, 0);
        AddToGrid(grid, Progress, 0, 1);
        return grid;
    }

    private void AddSection(Control control, int row)
    {
        Grid.SetRow(control, row);
        Children.Add(control);
    }

    private static string Text(
        UserInterfaceLanguage language,
        string japanese,
        string english
    ) => UserInterfaceText.Select(language, japanese, english);

    private static DataGridTextColumn CreateTextColumn(string header, string propertyName) =>
        new() { Header = header, Binding = new Binding(propertyName) };

    private static void AddToGrid(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }
}
