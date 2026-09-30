using Avalonia.Automation;
using Avalonia.Controls;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal sealed class MainWindowMenu : Menu
{
    protected override Type StyleKeyOverride => typeof(Menu);

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
        MinHeight = 28;

        AddItems(
            FileMenu,
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
            ExitItem
        );

        AddItems(AnalyzeMenu, AnalyzeItem, CancelItem, new Separator(), OpenSourceItem);

        AddItems(LanguageMenu, JapaneseLanguageItem, EnglishLanguageItem);
        AddItems(ThemeMenu, DarkThemeItem, LightThemeItem);
        AddItems(
            ViewMenu,
            SearchItem,
            new Separator(),
            DangerFilterItem,
            WarningFilterItem,
            AttentionFilterItem,
            new Separator(),
            LanguageMenu,
            ThemeMenu
        );

        AddItems(this, FileMenu, AnalyzeMenu, ViewMenu);

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

        AnalyzeItem.InputGesture = GuiCommands.Analyze.PrimaryShortcut?.ToKeyGesture();
        CancelItem.InputGesture = GuiCommands.Cancel.PrimaryShortcut?.ToKeyGesture();
        SearchItem.InputGesture = GuiCommands.Search.PrimaryShortcut?.ToKeyGesture();

        ConfigureAutomation();
        ApplyLanguage(UserInterfaceLanguage.Japanese);
    }

    internal void ApplyLanguage(UserInterfaceLanguage language)
    {
        SetTopLevelHeader(
            FileMenu,
            GuiText.Get(GuiTextKey.FileMenu, language),
            GuiCommands.FileMenu,
            language
        );
        SetTopLevelHeader(
            AnalyzeMenu,
            GuiText.Get(GuiTextKey.AnalyzeMenu, language),
            GuiCommands.AnalyzeMenu,
            language
        );
        SetTopLevelHeader(
            ViewMenu,
            GuiText.Get(GuiTextKey.ViewMenu, language),
            GuiCommands.ViewMenu,
            language
        );

        SetHeader(
            TargetFileItem,
            GuiText.Get(GuiTextKey.FileButton, language),
            GuiCommands.TargetFile,
            language
        );
        SetHeader(
            TargetFolderItem,
            GuiText.Get(GuiTextKey.FolderButton, language),
            GuiCommands.TargetFolder,
            language
        );
        SetHeader(
            ConfigurationItem,
            GuiText.Get(GuiTextKey.ConfigurationButton, language),
            GuiCommands.PickConfiguration,
            language
        );
        SetHeader(
            EditConfigurationItem,
            GuiText.Get(GuiTextKey.EditButton, language),
            GuiCommands.EditConfiguration,
            language
        );
        SetHeader(
            ClearConfigurationItem,
            GuiText.Get(GuiTextKey.ClearButton, language),
            GuiCommands.ClearConfiguration,
            language
        );
        SetHeader(
            ExportJsonItem,
            GuiText.Get(GuiTextKey.ExportJsonButton, language),
            GuiCommands.ExportJson,
            language
        );
        SetHeader(
            ExportSarifItem,
            GuiText.Get(GuiTextKey.ExportSarifButton, language),
            GuiCommands.ExportSarif,
            language
        );
        SetHeader(ExitItem, GuiText.Get(GuiTextKey.ExitMenu, language), GuiCommands.Exit, language);

        SetHeader(
            AnalyzeItem,
            GuiText.Get(GuiTextKey.AnalyzeButton, language),
            GuiCommands.Analyze,
            language
        );
        SetHeader(
            CancelItem,
            GuiText.Get(GuiTextKey.CancelButton, language),
            GuiCommands.Cancel,
            language
        );
        SetHeader(
            OpenSourceItem,
            GuiText.Get(GuiTextKey.OpenSourceButton, language),
            GuiCommands.OpenSource,
            language
        );

        SetHeader(
            SearchItem,
            GuiText.Get(GuiTextKey.SearchMenu, language),
            GuiCommands.Search,
            language
        );
        SetHeader(
            DangerFilterItem,
            GuiText.Get(GuiTextKey.Danger, language),
            GuiCommands.DangerFilter,
            language
        );
        SetHeader(
            WarningFilterItem,
            GuiText.Get(GuiTextKey.Warning, language),
            GuiCommands.WarningFilter,
            language
        );
        SetHeader(
            AttentionFilterItem,
            GuiText.Get(GuiTextKey.Attention, language),
            GuiCommands.AttentionFilter,
            language
        );
        SetHeader(
            LanguageMenu,
            GuiText.Get(GuiTextKey.LanguageLabel, language).TrimEnd(':'),
            GuiCommands.LanguageMenu,
            language
        );
        SetHeader(
            JapaneseLanguageItem,
            GuiText.Get(GuiTextKey.JapaneseLanguage, language),
            GuiCommands.JapaneseLanguage,
            language
        );
        SetHeader(
            EnglishLanguageItem,
            GuiText.Get(GuiTextKey.EnglishLanguage, language),
            GuiCommands.EnglishLanguage,
            language
        );
        SetHeader(
            ThemeMenu,
            GuiText.Get(GuiTextKey.ThemeLabel, language).TrimEnd(':'),
            GuiCommands.ThemeMenu,
            language
        );
        SetHeader(
            DarkThemeItem,
            GuiText.Get(GuiTextKey.DarkTheme, language),
            GuiCommands.DarkTheme,
            language
        );
        SetHeader(
            LightThemeItem,
            GuiText.Get(GuiTextKey.LightTheme, language),
            GuiCommands.LightTheme,
            language
        );
    }

    private static void AddItems(ItemsControl parent, params object[] items)
    {
        foreach (var item in items)
        {
            parent.Items.Add(item);
        }
    }

    private void ConfigureAutomation()
    {
        var items = new (MenuItem Item, string Id)[]
        {
            (FileMenu, "FileMenu"),
            (AnalyzeMenu, "AnalyzeMenu"),
            (ViewMenu, "ViewMenu"),
            (TargetFileItem, "TargetFileMenuItem"),
            (TargetFolderItem, "TargetFolderMenuItem"),
            (ConfigurationItem, "ConfigurationMenuItem"),
            (EditConfigurationItem, "EditConfigurationMenuItem"),
            (ClearConfigurationItem, "ClearConfigurationMenuItem"),
            (ExportJsonItem, "ExportJsonMenuItem"),
            (ExportSarifItem, "ExportSarifMenuItem"),
            (ExitItem, "ExitMenuItem"),
            (AnalyzeItem, "AnalyzeMenuItem"),
            (CancelItem, "CancelMenuItem"),
            (OpenSourceItem, "OpenSourceMenuItem"),
            (SearchItem, "SearchMenuItem"),
            (DangerFilterItem, "DangerFilterMenuItem"),
            (WarningFilterItem, "WarningFilterMenuItem"),
            (AttentionFilterItem, "AttentionFilterMenuItem"),
            (LanguageMenu, "LanguageMenu"),
            (JapaneseLanguageItem, "JapaneseLanguageMenuItem"),
            (EnglishLanguageItem, "EnglishLanguageMenuItem"),
            (ThemeMenu, "ThemeMenu"),
            (DarkThemeItem, "DarkThemeMenuItem"),
            (LightThemeItem, "LightThemeMenuItem"),
        };

        AutomationProperties.SetAutomationId(this, "MainMenu");
        foreach (var (item, id) in items)
        {
            AutomationProperties.SetAutomationId(item, id);
        }
    }

    private static void SetTopLevelHeader(
        MenuItem item,
        string text,
        GuiCommandDefinition command,
        UserInterfaceLanguage language
    )
    {
        var accessKey = AccessKey(command);
        var header =
            language == UserInterfaceLanguage.English
                ? PrefixAccessKey(text, accessKey)
                : $"{text} (_{accessKey})";
        item.Header = header;
        AutomationProperties.SetName(item, text);
        command.ApplyHelp(item, language);
    }

    private static void SetHeader(
        MenuItem item,
        string text,
        GuiCommandDefinition command,
        UserInterfaceLanguage language
    )
    {
        var accessKey = AccessKey(command);
        item.Header = $"{text} (_{accessKey})";
        AutomationProperties.SetName(item, text);
        command.ApplyHelp(item, language);
    }

    private static string AccessKey(GuiCommandDefinition command) =>
        command.MenuAccessKey is { } key
            ? GuiShortcut.FormatKey(key)
            : throw new InvalidOperationException($"Command {command.Id} has no menu access key.");

    private static string PrefixAccessKey(string text, string accessKey)
    {
        var index = text.IndexOf(accessKey, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? $"{text} (_{accessKey})" : text.Insert(index, "_");
    }
}
