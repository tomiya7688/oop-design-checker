using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OopDesignChecker.Configuration;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal sealed class 設定編集画面 : Window
{
    private readonly TextBox _編集欄 = new();
    private readonly TextBlock _状態欄 = new();
    private readonly UserInterfaceLanguage _表示言語;

    public 設定編集画面(string initialJson, UserInterfaceLanguage language)
    {
        _表示言語 = language;
        Title = GUI文言.取得する(GUI文言キー.編集画面タイトル, language);
        Width = 760;
        Height = 620;
        MinWidth = 560;
        MinHeight = 420;
        AutomationProperties.SetAutomationId(this, "ConfigurationEditorWindow");

        _編集欄.Text = initialJson;
        _編集欄.AcceptsReturn = true;
        _編集欄.TextWrapping = TextWrapping.NoWrap;
        AutomationProperties.SetAutomationId(_編集欄, "ConfigurationEditor");
        AutomationProperties.SetAutomationId(_状態欄, "ConfigurationEditorStatus");

        Content = 内容を構築する();
        KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape)
            {
                return;
            }

            args.Handled = true;
            Close(null);
        };
    }

    private Grid 内容を構築する()
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
            Text = GUI文言.取得する(GUI文言キー.編集画面説明, _表示言語),
            TextWrapping = TextWrapping.Wrap,
        };
        Add(root, description, 0);
        Add(root, _編集欄, 1);
        Add(root, _状態欄, 2);
        Add(root, 操作欄を構築する(), 3);
        return root;
    }

    private StackPanel 操作欄を構築する()
    {
        var validateButton = new Button
        {
            Content = GUI文言.取得する(GUI文言キー.検証ボタン, _表示言語),
        };
        var saveButton = new Button
        {
            Content = GUI文言.取得する(GUI文言キー.保存ボタン, _表示言語),
        };
        var cancelButton = new Button
        {
            Content = GUI文言.取得する(GUI文言キー.キャンセルボタン, _表示言語),
        };

        AutomationProperties.SetAutomationId(validateButton, "ValidateConfigurationButton");
        AutomationProperties.SetAutomationId(saveButton, "SaveConfigurationButton");
        AutomationProperties.SetAutomationId(cancelButton, "CancelConfigurationButton");

        validateButton.Click += (_, _) => 編集内容を検証する();
        saveButton.Click += (_, _) => 保存して閉じる();
        cancelButton.Click += (_, _) => Close(null);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { validateButton, saveButton, cancelButton },
        };
    }

    private void 編集内容を検証する()
    {
        try
        {
            _ = CheckerConfigurationJson.Parse(_編集欄.Text ?? string.Empty);
            _状態欄.Text = GUI文言.取得する(GUI文言キー.設定有効, _表示言語);
        }
        catch (InvalidOperationException)
        {
            _状態欄.Text = GUI文言.取得する(GUI文言キー.操作失敗, _表示言語);
        }
    }

    private void 保存して閉じる()
    {
        var json = _編集欄.Text ?? string.Empty;
        try
        {
            _ = CheckerConfigurationJson.Parse(json);
            Close(json);
        }
        catch (InvalidOperationException)
        {
            _状態欄.Text = GUI文言.取得する(GUI文言キー.操作失敗, _表示言語);
        }
    }

    private static void Add(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }
}
