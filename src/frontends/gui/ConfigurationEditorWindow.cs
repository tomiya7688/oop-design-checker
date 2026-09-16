using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OopDesignChecker.Configuration;

namespace OopDesignChecker.Gui;

internal sealed class ConfigurationEditorWindow : Window
{
    private readonly TextBox _editor = new();
    private readonly TextBlock _status = new();

    public ConfigurationEditorWindow(string initialJson)
    {
        Title = "OOP Design Checker configuration";
        Width = 760;
        Height = 620;
        MinWidth = 560;
        MinHeight = 420;

        _editor.Text = initialJson;
        _editor.AcceptsReturn = true;
        _editor.TextWrapping = TextWrapping.NoWrap;
        _editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

        Content = BuildContent();
    }

    private Grid BuildContent()
    {
        var root = new Grid
        {
            Margin = new Thickness(16),
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };

        var description = new TextBlock
        {
            Text = "Edit oop-design-checker.json. Validation uses the same configuration model as the CUI.",
            TextWrapping = TextWrapping.Wrap,
        };
        Add(root, description, 0);
        Add(root, _editor, 1);
        Add(root, _status, 2);
        Add(root, BuildActions(), 3);
        return root;
    }

    private StackPanel BuildActions()
    {
        var validateButton = new Button { Content = "Validate" };
        var saveButton = new Button { Content = "Save" };
        var cancelButton = new Button { Content = "Cancel" };

        validateButton.Click += (_, _) => ValidateEditor();
        saveButton.Click += (_, _) => SaveAndClose();
        cancelButton.Click += (_, _) => Close(null);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { validateButton, saveButton, cancelButton },
        };
    }

    private void ValidateEditor()
    {
        try
        {
            _ = CheckerConfigurationJson.Parse(_editor.Text ?? string.Empty);
            _status.Text = "Configuration is valid.";
        }
        catch (InvalidOperationException exception)
        {
            _status.Text = exception.Message;
        }
    }

    private void SaveAndClose()
    {
        var json = _editor.Text ?? string.Empty;
        try
        {
            _ = CheckerConfigurationJson.Parse(json);
            Close(json);
        }
        catch (InvalidOperationException exception)
        {
            _status.Text = exception.Message;
        }
    }

    private static void Add(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }
}
