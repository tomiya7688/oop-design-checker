using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal sealed class MainWindowMenu : Menu
{
    internal MenuItem FileMenu { get; } = new();
    internal MenuItem AnalyzeMenu { get; } = new();
    internal MenuItem ViewMenu { get; } = new();

    internal MenuItem TargetFileItem { get; } = new();
    internal MenuItem TargetFolderItem { get; } = new();
    internal MenuItem ConfigurationItem { get; } = new();
    internal MenuItem EditConfigurationItem { get; } = new();
    internal MenuItem ClearConfigurationItem { get; } = new();
    internal MenuItem ExportJsonItem { get; } = new();
    internal MenuItem ExportSarifItem { get; } = new();
    internal MenuItem ExitItem { get; } = new();

    internal MenuItem AnalyzeItem { get; } = new();
    internal MenuItem CancelItem { get; } = new();
    internal MenuItem OpenSourceItem { get; } = new();

    internal MenuItem SearchItem { get; } = new();
    internal MenuItem DangerFilterItem { get; } = new();
    internal MenuItem WarningFilterItem { get; } = new();
    internal MenuItem AttentionFilterItem { get; } = new();
    internal MenuItem LanguageMenu { get; } = new();
    internal MenuItem JapaneseLanguageItem { get; } = new();
    internal MenuItem EnglishLanguageItem { get; } = new();
    internal MenuItem ThemeMenu { get; } = new();
    internal MenuItem DarkThemeItem { get; } = new();
    internal MenuItem LightThemeItem { get; } = new();

    public MainWindowMenu()
    {
        FileMenu.ItemsSource = new object[]
        {
            TargetFileItem,
            TargetFolderItem,
            new Separator(),
            ConfigurationItem,
            EditConfigurationItem,
            ClearConfigurationItem,
            new Separator(),
            ExportJsonItem,
            ExportSarifItem,
            new Separator(),
            ExitItem,
        };

        AnalyzeMenu.ItemsSource = new object[]
        {
            AnalyzeItem,
            CancelItem,
            new Separator(),
            OpenSourceItem,
        };

        LanguageMenu.ItemsSource = new object[] { JapaneseLanguageItem, EnglishLanguageItem };
        ThemeMenu.ItemsSource = new object[] { DarkThemeItem, LightThemeItem };
        ViewMenu.ItemsSource = new object[]
        {
            SearchItem,
            new Separator(),
            DangerFilterItem,
            WarningFilterItem,
            AttentionFilterItem,
            new Separator(),
            LanguageMenu,
            ThemeMenu,
        };

        ItemsSource = new object[] { FileMenu, AnalyzeMenu, ViewMenu };

        DangerFilterItem.ToggleType = MenuItemToggleType.CheckBox;
        WarningFilterItem.ToggleType = MenuItemToggleType.CheckBox;
        AttentionFilterItem.ToggleType = MenuItemToggleType.CheckBox;

        JapaneseLanguageItem.ToggleType = MenuItemToggleType.Radio;
        JapaneseLanguageItem.GroupName = "Language";
        EnglishLanguageItem.ToggleType = MenuItemToggleType.Radio;
        EnglishLanguageItem.GroupName = "Language";

        DarkThemeItem.ToggleType = MenuItemToggleType.Radio;
        DarkThemeItem.GroupName = "Theme";
        LightThemeItem.ToggleType = MenuItemToggleType.Radio;
        LightThemeItem.GroupName = "Theme";

        AnalyzeItem.InputGesture = new KeyGesture(Key.F5);
        CancelItem.InputGesture = new KeyGesture(Key.Escape);
        SearchItem.InputGesture = new KeyGesture(Key.F, KeyModifiers.Control);

        ConfigureAutomation();
        ApplyLanguage(UserInterfaceLanguage.Japanese);
    }

    internal void ApplyLanguage(UserInterfaceLanguage language)
    {
        SetTopLevelHeader(FileMenu, GuiText.Get(GuiTextKey.FileMenu, language), 'F', language);
        SetTopLevelHeader(
            AnalyzeMenu,
            GuiText.Get(GuiTextKey.AnalyzeMenu, language),
            'A',
            language
        );
        SetTopLevelHeader(ViewMenu, GuiText.Get(GuiTextKey.ViewMenu, language), 'V', language);

        SetHeader(TargetFileItem, GuiText.Get(GuiTextKey.FileButton, language), 'T');
        SetHeader(TargetFolderItem, GuiText.Get(GuiTextKey.FolderButton, language), 'D');
        SetHeader(ConfigurationItem, GuiText.Get(GuiTextKey.ConfigurationButton, language), 'C');
        SetHeader(EditConfigurationItem, GuiText.Get(GuiTextKey.EditButton, language), 'E');
        SetHeader(ClearConfigurationItem, GuiText.Get(GuiTextKey.ClearButton, language), 'L');
        SetHeader(ExportJsonItem, GuiText.Get(GuiTextKey.ExportJsonButton, language), 'J');
        SetHeader(ExportSarifItem, GuiText.Get(GuiTextKey.ExportSarifButton, language), 'S');
        SetHeader(ExitItem, GuiText.Get(GuiTextKey.ExitMenu, language), 'X');

        SetHeader(AnalyzeItem, GuiText.Get(GuiTextKey.AnalyzeButton, language), 'R');
        SetHeader(CancelItem, GuiText.Get(GuiTextKey.CancelButton, language), 'C');
        SetHeader(OpenSourceItem, GuiText.Get(GuiTextKey.OpenSourceButton, language), 'O');

        SetHeader(SearchItem, GuiText.Get(GuiTextKey.SearchMenu, language), 'F');
        SetHeader(DangerFilterItem, GuiText.Get(GuiTextKey.Danger, language), 'D');
        SetHeader(WarningFilterItem, GuiText.Get(GuiTextKey.Warning, language), 'W');
        SetHeader(AttentionFilterItem, GuiText.Get(GuiTextKey.Attention, language), 'A');
        SetHeader(LanguageMenu, GuiText.Get(GuiTextKey.LanguageLabel, language).TrimEnd(':'), 'L');
        SetHeader(JapaneseLanguageItem, GuiText.Get(GuiTextKey.JapaneseLanguage, language), 'J');
        SetHeader(EnglishLanguageItem, GuiText.Get(GuiTextKey.EnglishLanguage, language), 'E');
        SetHeader(ThemeMenu, GuiText.Get(GuiTextKey.ThemeLabel, language).TrimEnd(':'), 'T');
        SetHeader(DarkThemeItem, GuiText.Get(GuiTextKey.DarkTheme, language), 'D');
        SetHeader(LightThemeItem, GuiText.Get(GuiTextKey.LightTheme, language), 'L');
    }

    private void ConfigureAutomation()
    {
        var items = new (MenuItem Item, string Id, string? AccessKey)[]
        {
            (FileMenu, "FileMenu", "Alt+F"),
            (AnalyzeMenu, "AnalyzeMenu", "Alt+A"),
            (ViewMenu, "ViewMenu", "Alt+V"),
            (TargetFileItem, "TargetFileMenuItem", null),
            (TargetFolderItem, "TargetFolderMenuItem", null),
            (ConfigurationItem, "ConfigurationMenuItem", null),
            (EditConfigurationItem, "EditConfigurationMenuItem", null),
            (ClearConfigurationItem, "ClearConfigurationMenuItem", null),
            (ExportJsonItem, "ExportJsonMenuItem", null),
            (ExportSarifItem, "ExportSarifMenuItem", null),
            (ExitItem, "ExitMenuItem", null),
            (AnalyzeItem, "AnalyzeMenuItem", "F5"),
            (CancelItem, "CancelMenuItem", "Escape"),
            (OpenSourceItem, "OpenSourceMenuItem", null),
            (SearchItem, "SearchMenuItem", "Ctrl+F"),
            (DangerFilterItem, "DangerFilterMenuItem", null),
            (WarningFilterItem, "WarningFilterMenuItem", null),
            (AttentionFilterItem, "AttentionFilterMenuItem", null),
            (LanguageMenu, "LanguageMenu", null),
            (JapaneseLanguageItem, "JapaneseLanguageMenuItem", null),
            (EnglishLanguageItem, "EnglishLanguageMenuItem", null),
            (ThemeMenu, "ThemeMenu", null),
            (DarkThemeItem, "DarkThemeMenuItem", null),
            (LightThemeItem, "LightThemeMenuItem", null),
        };

        AutomationProperties.SetAutomationId(this, "MainMenu");
        foreach (var (item, id, accessKey) in items)
        {
            AutomationProperties.SetAutomationId(item, id);
            if (accessKey is not null)
            {
                AutomationProperties.SetAccessKey(item, accessKey);
            }
        }
    }

    private static void SetTopLevelHeader(
        MenuItem item,
        string text,
        char accessKey,
        UserInterfaceLanguage language
    )
    {
        var header =
            language == UserInterfaceLanguage.English
                ? PrefixAccessKey(text, accessKey)
                : $"{text} (_{accessKey})";
        item.Header = header;
        AutomationProperties.SetName(item, text);
    }

    private static void SetHeader(MenuItem item, string text, char accessKey)
    {
        item.Header = $"{text} (_{accessKey})";
        AutomationProperties.SetName(item, text);
    }

    private static string PrefixAccessKey(string text, char accessKey)
    {
        var index = text.IndexOf(accessKey, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? $"{text} (_{accessKey})" : text.Insert(index, "_");
    }
}
