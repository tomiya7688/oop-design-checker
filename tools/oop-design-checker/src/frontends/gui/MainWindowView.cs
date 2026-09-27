using Avalonia;
using Avalonia.Automation;
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
    internal ProgressBar Progress { get; } = new();
    internal TextBlock Status { get; } = new();
    internal TextBlock DangerCount { get; } = new();
    internal TextBlock WarningCount { get; } = new();
    internal TextBlock AttentionCount { get; } = new();
    internal CheckBox DangerFilter { get; } = new();
    internal CheckBox WarningFilter { get; } = new();
    internal CheckBox AttentionFilter { get; } = new();
    internal TextBox SearchFilter { get; } = new();
    internal ComboBox LanguageSelector { get; } = new();
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

    private readonly TextBlock _showLabel = new();
    private readonly TextBlock _languageLabel = new();
    private readonly TextBlock _detailHeading = new();

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
        ConfigureAutomation();
        AddSection(BuildTargetSection(), 0);
        AddSection(BuildFilterSection(), 1);
        AddSection(BuildSummarySection(), 2);
        AddSection(BuildDiagnosticsSection(), 3);
        AddSection(BuildStatusSection(), 4);
        ApplyLanguage(UserInterfaceLanguage.Japanese);
    }

    internal void ApplyLanguage(UserInterfaceLanguage language)
    {
        TargetPath.PlaceholderText = GuiText.Get(GuiTextKey.TargetPlaceholder, language);
        ConfigurationPath.PlaceholderText = GuiText.Get(
            GuiTextKey.ConfigurationPlaceholder,
            language
        );

        TargetFileButton.Content = GuiText.Get(GuiTextKey.FileButton, language);
        TargetFolderButton.Content = GuiText.Get(GuiTextKey.FolderButton, language);
        ConfigurationButton.Content = GuiText.Get(GuiTextKey.ConfigurationButton, language);
        EditConfigurationButton.Content = GuiText.Get(GuiTextKey.EditButton, language);
        ClearConfigurationButton.Content = GuiText.Get(GuiTextKey.ClearButton, language);
        AnalyzeButton.Content = GuiText.Get(GuiTextKey.AnalyzeButton, language);
        CancelButton.Content = GuiText.Get(GuiTextKey.CancelButton, language);

        _showLabel.Text = GuiText.Get(GuiTextKey.ShowLabel, language);
        _languageLabel.Text = GuiText.Get(GuiTextKey.LanguageLabel, language);
        DangerFilter.Content = GuiText.Get(GuiTextKey.Danger, language);
        WarningFilter.Content = GuiText.Get(GuiTextKey.Warning, language);
        AttentionFilter.Content = GuiText.Get(GuiTextKey.Attention, language);
        SearchFilter.PlaceholderText = GuiText.Get(GuiTextKey.SearchPlaceholder, language);

        CopySelectedButton.Content = GuiText.Get(GuiTextKey.CopySelectedButton, language);
        CopyAllButton.Content = GuiText.Get(GuiTextKey.CopyAllButton, language);
        OpenSourceButton.Content = GuiText.Get(GuiTextKey.OpenSourceButton, language);
        ExportJsonButton.Content = GuiText.Get(GuiTextKey.ExportJsonButton, language);
        ExportSarifButton.Content = GuiText.Get(GuiTextKey.ExportSarifButton, language);
        _detailHeading.Text = GuiText.Get(GuiTextKey.DiagnosticDetails, language);

        DiagnosticsGrid.Columns[0].Header = GuiText.Get(GuiTextKey.SeverityColumn, language);
        DiagnosticsGrid.Columns[1].Header = GuiText.Get(GuiTextKey.RuleColumn, language);
        DiagnosticsGrid.Columns[2].Header = GuiText.Get(GuiTextKey.FileColumn, language);
        DiagnosticsGrid.Columns[3].Header = GuiText.Get(GuiTextKey.LineColumn, language);
        DiagnosticsGrid.Columns[4].Header = GuiText.Get(GuiTextKey.ColumnColumn, language);
        DiagnosticsGrid.Columns[5].Header = GuiText.Get(GuiTextKey.MessageColumn, language);

        ClearDetails(language);
    }

    private void ConfigureAutomation()
    {
        var controls = new (StyledElement Element, string Id)[]
        {
            (TargetPath, "TargetPath"),
            (ConfigurationPath, "ConfigurationPath"),
            (TargetFileButton, "TargetFileButton"),
            (TargetFolderButton, "TargetFolderButton"),
            (ConfigurationButton, "ConfigurationButton"),
            (EditConfigurationButton, "EditConfigurationButton"),
            (ClearConfigurationButton, "ClearConfigurationButton"),
            (AnalyzeButton, "AnalyzeButton"),
            (CancelButton, "CancelButton"),
            (Progress, "AnalysisProgress"),
            (Status, "Status"),
            (DangerCount, "DangerCount"),
            (WarningCount, "WarningCount"),
            (AttentionCount, "AttentionCount"),
            (DangerFilter, "DangerFilter"),
            (WarningFilter, "WarningFilter"),
            (AttentionFilter, "AttentionFilter"),
            (SearchFilter, "SearchFilter"),
            (LanguageSelector, "LanguageSelector"),
            (DiagnosticsGrid, "DiagnosticsGrid"),
            (DetailSeverity, "DetailSeverity"),
            (DetailRule, "DetailRule"),
            (DetailSymbol, "DetailSymbol"),
            (DetailLocation, "DetailLocation"),
            (DetailMessage, "DetailMessage"),
            (ConfigurationSummary, "ConfigurationSummary"),
            (CopySelectedButton, "CopySelectedButton"),
            (CopyAllButton, "CopyAllButton"),
            (OpenSourceButton, "OpenSourceButton"),
            (ExportJsonButton, "ExportJsonButton"),
            (ExportSarifButton, "ExportSarifButton"),
        };

        foreach (var (element, id) in controls)
        {
            AutomationProperties.SetAutomationId(element, id);
        }
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

        DiagnosticsGrid.Columns.Add(
            CreateTextColumn(string.Empty, nameof(DiagnosticRow.SeverityText))
        );
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
        _showLabel.VerticalAlignment = VerticalAlignment.Center;
        _languageLabel.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(_showLabel);
        panel.Children.Add(DangerFilter);
        panel.Children.Add(WarningFilter);
        panel.Children.Add(AttentionFilter);
        panel.Children.Add(SearchFilter);
        panel.Children.Add(AnalyzeButton);
        panel.Children.Add(CancelButton);
        panel.Children.Add(_languageLabel);
        panel.Children.Add(LanguageSelector);
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
        _detailHeading.FontSize = 18;
        panel.Children.Add(_detailHeading);
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

    internal void ClearDetails(UserInterfaceLanguage language)
    {
        DetailSeverity.Text = GuiText.Format(GuiTextKey.SeverityDetail, language, "-");
        DetailRule.Text = GuiText.Format(GuiTextKey.RuleDetail, language, "-");
        DetailSymbol.Text = GuiText.Format(GuiTextKey.SymbolDetail, language, "-");
        DetailLocation.Text = GuiText.Format(GuiTextKey.LocationDetail, language, "-");
        DetailMessage.Text = GuiText.Get(GuiTextKey.SelectDiagnostic, language);
    }

    private void AddSection(Control control, int row)
    {
        Grid.SetRow(control, row);
        Children.Add(control);
    }

    private static DataGridTextColumn CreateTextColumn(string header, string propertyName) =>
        new() { Header = header, Binding = new Binding(propertyName) };

    private static void AddToGrid(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }
}
