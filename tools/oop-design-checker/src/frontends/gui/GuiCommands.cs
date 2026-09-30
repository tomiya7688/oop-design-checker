using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal readonly record struct GuiShortcut(Key Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    internal bool Matches(KeyEventArgs args) => args.Key == Key && args.KeyModifiers == Modifiers;

    internal KeyGesture ToKeyGesture() => new(Key, Modifiers);

    internal string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(KeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(KeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(KeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            parts.Add(FormatKey(Key));
            return string.Join("+", parts);
        }
    }

    internal static string FormatKey(Key key) =>
        key switch
        {
            Key.Escape => "Esc",
            _ => key.ToString(),
        };
}

internal sealed record GuiCommandDefinition(
    string Id,
    GuiShortcut? PrimaryShortcut,
    GuiShortcut? AlternateShortcut,
    IReadOnlyList<Key> MenuPath,
    string JapaneseDescription,
    string EnglishDescription
)
{
    internal bool Matches(KeyEventArgs args) =>
        PrimaryShortcut?.Matches(args) == true || AlternateShortcut?.Matches(args) == true;

    internal string Description(UserInterfaceLanguage language) =>
        language == UserInterfaceLanguage.Japanese ? JapaneseDescription : EnglishDescription;

    internal string DirectShortcutText =>
        string.Join(
            " / ",
            new[] { PrimaryShortcut, AlternateShortcut }
                .Where(shortcut => shortcut is not null)
                .Select(shortcut => shortcut!.Value.DisplayText)
        );

    internal string MenuPathText =>
        MenuPath.Count == 0
            ? string.Empty
            : string.Join(
                " → ",
                MenuPath.Select(
                    (key, index) =>
                        index == 0
                            ? $"Alt+{GuiShortcut.FormatKey(key)}"
                            : GuiShortcut.FormatKey(key)
                )
            );

    internal string ShortcutText =>
        string.Join(
            " · ",
            new[] { DirectShortcutText, MenuPathText }.Where(text =>
                !string.IsNullOrWhiteSpace(text)
            )
        );

    internal string PrimaryHint =>
        PrimaryShortcut?.DisplayText ?? (MenuPath.Count == 1 ? MenuPathText : string.Empty);

    internal Key? MenuAccessKey => MenuPath.Count == 0 ? null : MenuPath[^1];

    internal string HelpText(UserInterfaceLanguage language)
    {
        var description = Description(language);
        if (string.IsNullOrWhiteSpace(ShortcutText))
        {
            return description;
        }

        var keyboardLabel = language == UserInterfaceLanguage.Japanese ? "キーボード" : "Keyboard";
        return $"{description}{Environment.NewLine}{keyboardLabel}: {ShortcutText}";
    }

    internal void ApplyHelp(Control control, UserInterfaceLanguage language)
    {
        var helpText = HelpText(language);
        ToolTip.SetTip(control, helpText);
        AutomationProperties.SetHelpText(control, helpText);

        var accessKey =
            PrimaryShortcut?.DisplayText
            ?? (!string.IsNullOrWhiteSpace(MenuPathText) ? MenuPathText : null);
        if (accessKey is not null)
        {
            AutomationProperties.SetAccessKey(control, accessKey);
        }
    }
}

internal static class GuiCommands
{
    internal static GuiCommandDefinition Analyze { get; } =
        new(
            "analyze",
            new GuiShortcut(Key.F5),
            new GuiShortcut(Key.Enter, KeyModifiers.Control),
            [Key.A, Key.R],
            "解析を開始します。",
            "Start analysis."
        );

    internal static GuiCommandDefinition Cancel { get; } =
        new(
            "cancel",
            new GuiShortcut(Key.Escape),
            null,
            [Key.A, Key.C],
            "Escは入力やメニューを閉じ、解析中はキャンセルします。",
            "Escape leaves editing or closes menus; while analyzing it cancels."
        );

    internal static GuiCommandDefinition Search { get; } =
        new(
            "search",
            new GuiShortcut(Key.F, KeyModifiers.Control),
            null,
            [Key.V, Key.F],
            "診断検索へフォーカスします。",
            "Focus diagnostic search."
        );

    internal static GuiCommandDefinition TargetFocus { get; } =
        new(
            "target-focus",
            new GuiShortcut(Key.T, KeyModifiers.Alt),
            null,
            [],
            "解析対象pathへフォーカスします。",
            "Focus the analysis target path."
        );

    internal static GuiCommandDefinition ConfigurationFocus { get; } =
        new(
            "configuration-focus",
            new GuiShortcut(Key.C, KeyModifiers.Alt),
            null,
            [],
            "checker設定pathへフォーカスします。",
            "Focus the checker configuration path."
        );

    internal static GuiCommandDefinition FileMenu { get; } =
        new(
            "file-menu",
            null,
            null,
            [Key.F],
            "ファイルメニューを開きます。",
            "Open the File menu."
        );

    internal static GuiCommandDefinition AnalyzeMenu { get; } =
        new(
            "analyze-menu",
            null,
            null,
            [Key.A],
            "解析メニューを開きます。",
            "Open the Analyze menu."
        );

    internal static GuiCommandDefinition ViewMenu { get; } =
        new(
            "view-menu",
            null,
            null,
            [Key.V],
            "表示メニューを開きます。",
            "Open the View menu."
        );

    internal static GuiCommandDefinition TargetFile { get; } =
        new(
            "target-file",
            null,
            null,
            [Key.F, Key.T],
            "解析対象ファイルを選択します。",
            "Choose an analysis target file."
        );

    internal static GuiCommandDefinition TargetFolder { get; } =
        new(
            "target-folder",
            null,
            null,
            [Key.F, Key.D],
            "解析対象フォルダーを選択します。",
            "Choose an analysis target folder."
        );

    internal static GuiCommandDefinition PickConfiguration { get; } =
        new(
            "configuration-pick",
            null,
            null,
            [Key.F, Key.C],
            "checker設定ファイルを選択します。",
            "Choose a checker configuration file."
        );

    internal static GuiCommandDefinition EditConfiguration { get; } =
        new(
            "configuration-edit",
            null,
            null,
            [Key.F, Key.E],
            "checker設定を編集します。",
            "Edit the checker configuration."
        );

    internal static GuiCommandDefinition ClearConfiguration { get; } =
        new(
            "configuration-clear",
            null,
            null,
            [Key.F, Key.L],
            "checker設定pathをクリアします。",
            "Clear the checker configuration path."
        );

    internal static GuiCommandDefinition ExportJson { get; } =
        new(
            "export-json",
            null,
            null,
            [Key.F, Key.J],
            "診断をJSONで出力します。",
            "Export diagnostics as JSON."
        );

    internal static GuiCommandDefinition ExportSarif { get; } =
        new(
            "export-sarif",
            null,
            null,
            [Key.F, Key.S],
            "診断をSARIFで出力します。",
            "Export diagnostics as SARIF."
        );

    internal static GuiCommandDefinition Exit { get; } =
        new(
            "exit",
            null,
            null,
            [Key.F, Key.X],
            "GUIを終了します。",
            "Exit the GUI."
        );

    internal static GuiCommandDefinition OpenSource { get; } =
        new(
            "open-source",
            null,
            null,
            [Key.A, Key.O],
            "選択した診断のsourceを開きます。",
            "Open the selected diagnostic source."
        );

    internal static GuiCommandDefinition CopySelected { get; } =
        new(
            "copy-selected",
            new GuiShortcut(Key.C, KeyModifiers.Control),
            null,
            [],
            "選択した診断をコピーします。",
            "Copy the selected diagnostic."
        );

    internal static GuiCommandDefinition CopyAll { get; } =
        new(
            "copy-all",
            null,
            null,
            [],
            "すべての診断をコピーします。",
            "Copy all diagnostics."
        );

    internal static GuiCommandDefinition DangerFilter { get; } =
        new(
            "danger-filter",
            null,
            null,
            [Key.V, Key.D],
            "Danger診断の表示を切り替えます。",
            "Toggle Danger diagnostics."
        );

    internal static GuiCommandDefinition WarningFilter { get; } =
        new(
            "warning-filter",
            null,
            null,
            [Key.V, Key.W],
            "Warning診断の表示を切り替えます。",
            "Toggle Warning diagnostics."
        );

    internal static GuiCommandDefinition AttentionFilter { get; } =
        new(
            "attention-filter",
            null,
            null,
            [Key.V, Key.A],
            "Attention診断の表示を切り替えます。",
            "Toggle Attention diagnostics."
        );

    internal static GuiCommandDefinition LanguageMenu { get; } =
        new(
            "language-menu",
            null,
            null,
            [Key.V, Key.L],
            "表示言語メニューを開きます。",
            "Open the UI language menu."
        );

    internal static GuiCommandDefinition ThemeMenu { get; } =
        new(
            "theme-menu",
            null,
            null,
            [Key.V, Key.T],
            "テーマメニューを開きます。",
            "Open the theme menu."
        );

    internal static GuiCommandDefinition JapaneseLanguage { get; } =
        new(
            "language-ja",
            null,
            null,
            [Key.V, Key.L, Key.J],
            "表示言語を日本語へ切り替えます。",
            "Switch the UI language to Japanese."
        );

    internal static GuiCommandDefinition EnglishLanguage { get; } =
        new(
            "language-en",
            null,
            null,
            [Key.V, Key.L, Key.E],
            "表示言語を英語へ切り替えます。",
            "Switch the UI language to English."
        );

    internal static GuiCommandDefinition DarkTheme { get; } =
        new(
            "theme-dark",
            null,
            null,
            [Key.V, Key.T, Key.D],
            "ダークテーマへ切り替えます。",
            "Switch to the Dark theme."
        );

    internal static GuiCommandDefinition LightTheme { get; } =
        new(
            "theme-light",
            null,
            null,
            [Key.V, Key.T, Key.L],
            "ライトテーマへ切り替えます。",
            "Switch to the Light theme."
        );
}
