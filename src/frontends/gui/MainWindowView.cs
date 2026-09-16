using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace OopDesignChecker.Gui;

internal sealed class MainWindowView : Grid
{
    public TextBox TargetPath { get; } = new();
    public TextBox ConfigurationPath { get; } = new();
    public Button TargetFileButton { get; } = new();
    public Button TargetFolderButton { get; } = new();
    public Button ConfigurationButton { get; } = new();
    public Button ClearConfigurationButton { get; } = new();
    public Button AnalyzeButton { get; } = new();
    public ProgressBar Progress { get; } = new();
    public TextBlock Status { get; } = new();
    public TextBlock DangerCount { get; } = new();
    public TextBlock WarningCount { get; } = new();
    public TextBlock AttentionCount { get; } = new();
    public CheckBox DangerFilter { get; } = new();
    public CheckBox WarningFilter { get; } = new();
    public CheckBox AttentionFilter { get; } = new();
    public TextBox SearchFilter { get; } = new();
    public DataGrid DiagnosticsGrid { get; } = new();
    public TextBlock DetailSeverity { get; } = new();
    public TextBlock DetailRule { get; } = new();
    public TextBlock DetailSymbol { get; } = new();
    public TextBlock DetailLocation { get; } = new();
    public TextBlock DetailMessage { get; } = new();
    public TextBlock ConfigurationSummary { get; } = new();
    public Button CopySelectedButton { get; } = new();
    public Button CopyAllButton { get; } = new();
    public Button OpenSourceButton { get; } = new();

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

    private void ConfigureControls()
    {
        TargetPath.Text = Directory.GetCurrentDirectory();
        TargetPath.PlaceholderText = "Project, solution, C# file, or project directory";
        ConfigurationPath.PlaceholderText = "Optional oop-design-checker.json";

        TargetFileButton.Content = "File...";
        TargetFolderButton.Content = "Folder...";
        ConfigurationButton.Content = "Config...";
        ClearConfigurationButton.Content = "Clear";
        AnalyzeButton.Content = "Analyze";

        DangerFilter.Content = "Danger";
        DangerFilter.IsChecked = true;
        WarningFilter.Content = "Warning";
        WarningFilter.IsChecked = true;
        AttentionFilter.Content = "Attention";
        AttentionFilter.IsChecked = true;
        SearchFilter.PlaceholderText = "Filter by rule, file, symbol, or message";

        Progress.IsIndeterminate = true;
        Progress.IsVisible = false;
        Status.Text = "Ready";
        DetailMessage.TextWrapping = TextWrapping.Wrap;
        DetailLocation.TextWrapping = TextWrapping.Wrap;
        ConfigurationSummary.TextWrapping = TextWrapping.Wrap;

        ConfigureDiagnosticsGrid();
        ClearDetails();
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
            CreateTextColumn("Severity", nameof(DiagnosticRow.SeverityText))
        );
        DiagnosticsGrid.Columns.Add(CreateTextColumn("Rule", nameof(DiagnosticRow.RuleId)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn("File", nameof(DiagnosticRow.FileName)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn("Line", nameof(DiagnosticRow.Line)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn("Column", nameof(DiagnosticRow.Column)));
        DiagnosticsGrid.Columns.Add(CreateTextColumn("Message", nameof(DiagnosticRow.Message)));
    }

    private Control BuildTargetSection()
    {
        var section = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        section.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star))
        );
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        section.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        section.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddToGrid(section, TargetPath, 0, 0);
        AddToGrid(section, TargetFileButton, 0, 1);
        AddToGrid(section, TargetFolderButton, 0, 2);
        AddToGrid(section, ConfigurationPath, 1, 0);
        AddToGrid(section, ConfigurationButton, 1, 1);
        AddToGrid(section, ClearConfigurationButton, 1, 2);
        return section;
    }

    private Control BuildFilterSection()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(
            new TextBlock { Text = "Show:", VerticalAlignment = VerticalAlignment.Center }
        );
        panel.Children.Add(DangerFilter);
        panel.Children.Add(WarningFilter);
        panel.Children.Add(AttentionFilter);
        panel.Children.Add(SearchFilter);
        panel.Children.Add(AnalyzeButton);
        return panel;
    }

    private Control BuildSummarySection()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        panel.Children.Add(DangerCount);
        panel.Children.Add(WarningCount);
        panel.Children.Add(AttentionCount);
        panel.Children.Add(ConfigurationSummary);
        return panel;
    }

    private Control BuildDiagnosticsSection()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(3, GridUnitType.Star))
        );
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(2, GridUnitType.Star))
        );

        AddToGrid(grid, DiagnosticsGrid, 0, 0);
        AddToGrid(grid, BuildDetailPane(), 0, 1);
        return grid;
    }

    private Control BuildDetailPane()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = "Diagnostic details", FontSize = 18 });
        panel.Children.Add(DetailSeverity);
        panel.Children.Add(DetailRule);
        panel.Children.Add(DetailSymbol);
        panel.Children.Add(DetailLocation);
        panel.Children.Add(DetailMessage);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        CopySelectedButton.Content = "Copy selected";
        CopyAllButton.Content = "Copy all";
        OpenSourceButton.Content = "Open source";
        actions.Children.Add(CopySelectedButton);
        actions.Children.Add(CopyAllButton);
        actions.Children.Add(OpenSourceButton);
        panel.Children.Add(actions);

        return new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = panel,
        };
    }

    private Control BuildStatusSection()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star))
        );
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
        AddToGrid(grid, Status, 0, 0);
        AddToGrid(grid, Progress, 0, 1);
        return grid;
    }

    public void ClearDetails()
    {
        DetailSeverity.Text = "Severity: -";
        DetailRule.Text = "Rule: -";
        DetailSymbol.Text = "Symbol: -";
        DetailLocation.Text = "Location: -";
        DetailMessage.Text = "Select a diagnostic to see its full message.";
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
