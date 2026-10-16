using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal readonly record struct GUIショートカット(
    Key Key,
    KeyModifiers Modifiers = KeyModifiers.None
)
{
    internal bool 一致する(KeyEventArgs args) => args.Key == Key && args.KeyModifiers == Modifiers;

    internal KeyGesture キー操作に変換する() => new(Key, Modifiers);

    internal string 表示文字列
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

            parts.Add(キー名を整形する(Key));
            return string.Join("+", parts);
        }
    }

    internal static string キー名を整形する(Key key) =>
        key switch
        {
            Key.Escape => "Esc",
            _ => key.ToString(),
        };
}

internal sealed record GUIコマンド定義(
    string 識別子,
    GUIショートカット? 主ショートカット,
    GUIショートカット? 代替ショートカット,
    IReadOnlyList<Key> メニュー経路,
    string 日本語説明,
    string 英語説明
)
{
    internal bool 一致する(KeyEventArgs args) =>
        主ショートカット?.一致する(args) == true || 代替ショートカット?.一致する(args) == true;

    internal string 説明文(UserInterfaceLanguage language) =>
        language == UserInterfaceLanguage.Japanese ? 日本語説明 : 英語説明;

    internal string 直接操作の表示 =>
        string.Join(
            " / ",
            new[] { 主ショートカット, 代替ショートカット }
                .Where(shortcut => shortcut is not null)
                .Select(shortcut => shortcut!.Value.表示文字列)
        );

    internal string メニュー経路の表示 =>
        メニュー経路.Count == 0
            ? string.Empty
            : string.Join(
                " → ",
                メニュー経路.Select(
                    (key, index) =>
                        index == 0
                            ? $"Alt+{GUIショートカット.キー名を整形する(key)}"
                            : GUIショートカット.キー名を整形する(key)
                )
            );

    internal string ショートカット表示 =>
        string.Join(
            " · ",
            new[] { 直接操作の表示, メニュー経路の表示 }.Where(text =>
                !string.IsNullOrWhiteSpace(text)
            )
        );

    internal string 主操作の案内 =>
        主ショートカット?.表示文字列
        ?? (メニュー経路.Count == 1 ? メニュー経路の表示 : string.Empty);

    internal Key? メニューアクセスキー => メニュー経路.Count == 0 ? null : メニュー経路[^1];

    internal string ヘルプ文(UserInterfaceLanguage language)
    {
        var description = 説明文(language);
        if (string.IsNullOrWhiteSpace(ショートカット表示))
        {
            return description;
        }

        var keyboardLabel = language == UserInterfaceLanguage.Japanese ? "キーボード" : "Keyboard";
        return $"{description}{Environment.NewLine}{keyboardLabel}: {ショートカット表示}";
    }

    internal void ヘルプを適用する(Control control, UserInterfaceLanguage language)
    {
        var helpText = ヘルプ文(language);
        ToolTip.SetTip(control, helpText);
        AutomationProperties.SetHelpText(control, helpText);

        var accessKey =
            主ショートカット?.表示文字列
            ?? (!string.IsNullOrWhiteSpace(メニュー経路の表示) ? メニュー経路の表示 : null);
        if (accessKey is not null)
        {
            AutomationProperties.SetAccessKey(control, accessKey);
        }
    }
}

internal static class GUIコマンド
{
    internal static GUIコマンド定義 解析開始 { get; } =
        new(
            "analyze",
            new GUIショートカット(Key.F5),
            new GUIショートカット(Key.Enter, KeyModifiers.Control),
            [Key.A, Key.R],
            "解析を開始します。",
            "Start analysis."
        );

    internal static GUIコマンド定義 解析キャンセル { get; } =
        new(
            "cancel",
            new GUIショートカット(Key.Escape),
            null,
            [Key.A, Key.C],
            "Escは入力やメニューを閉じ、解析中はキャンセルします。",
            "Escape leaves editing or closes menus; while analyzing it cancels."
        );

    internal static GUIコマンド定義 診断検索 { get; } =
        new(
            "search",
            new GUIショートカット(Key.F, KeyModifiers.Control),
            null,
            [Key.V, Key.F],
            "診断検索へフォーカスします。",
            "Focus diagnostic search."
        );

    internal static GUIコマンド定義 対象欄フォーカス { get; } =
        new(
            "target-focus",
            new GUIショートカット(Key.T, KeyModifiers.Alt),
            null,
            [],
            "解析対象pathへフォーカスします。",
            "Focus the analysis target path."
        );

    internal static GUIコマンド定義 設定欄フォーカス { get; } =
        new(
            "configuration-focus",
            new GUIショートカット(Key.C, KeyModifiers.Alt),
            null,
            [],
            "checker設定pathへフォーカスします。",
            "Focus the checker configuration path."
        );

    internal static GUIコマンド定義 ファイルメニュー { get; } =
        new(
            "file-menu",
            null,
            null,
            [Key.F],
            "ファイルメニューを開きます。",
            "Open the File menu."
        );

    internal static GUIコマンド定義 解析メニュー { get; } =
        new(
            "analyze-menu",
            null,
            null,
            [Key.A],
            "解析メニューを開きます。",
            "Open the Analyze menu."
        );

    internal static GUIコマンド定義 表示メニュー { get; } =
        new("view-menu", null, null, [Key.V], "表示メニューを開きます。", "Open the View menu.");

    internal static GUIコマンド定義 対象ファイル選択 { get; } =
        new(
            "target-file",
            null,
            null,
            [Key.F, Key.T],
            "解析対象ファイルを選択します。",
            "Choose an analysis target file."
        );

    internal static GUIコマンド定義 対象フォルダー選択 { get; } =
        new(
            "target-folder",
            null,
            null,
            [Key.F, Key.D],
            "解析対象フォルダーを選択します。",
            "Choose an analysis target folder."
        );

    internal static GUIコマンド定義 設定選択 { get; } =
        new(
            "configuration-pick",
            null,
            null,
            [Key.F, Key.C],
            "checker設定ファイルを選択します。",
            "Choose a checker configuration file."
        );

    internal static GUIコマンド定義 設定編集 { get; } =
        new(
            "configuration-edit",
            null,
            null,
            [Key.F, Key.E],
            "checker設定を編集します。",
            "Edit the checker configuration."
        );

    internal static GUIコマンド定義 設定消去 { get; } =
        new(
            "configuration-clear",
            null,
            null,
            [Key.F, Key.L],
            "checker設定pathをクリアします。",
            "Clear the checker configuration path."
        );

    internal static GUIコマンド定義 JSON出力 { get; } =
        new(
            "export-json",
            null,
            null,
            [Key.F, Key.J],
            "診断をJSONで出力します。",
            "Export diagnostics as JSON."
        );

    internal static GUIコマンド定義 SARIF出力 { get; } =
        new(
            "export-sarif",
            null,
            null,
            [Key.F, Key.S],
            "診断をSARIFで出力します。",
            "Export diagnostics as SARIF."
        );

    internal static GUIコマンド定義 終了 { get; } =
        new("exit", null, null, [Key.F, Key.X], "GUIを終了します。", "Exit the GUI.");

    internal static GUIコマンド定義 ソースを開く { get; } =
        new(
            "open-source",
            null,
            null,
            [Key.A, Key.O],
            "選択した診断のsourceを開きます。",
            "Open the selected diagnostic source."
        );

    internal static GUIコマンド定義 選択項目コピー { get; } =
        new(
            "copy-selected",
            new GUIショートカット(Key.C, KeyModifiers.Control),
            null,
            [],
            "選択した診断をコピーします。",
            "Copy the selected diagnostic."
        );

    internal static GUIコマンド定義 全件コピー { get; } =
        new("copy-all", null, null, [], "すべての診断をコピーします。", "Copy all diagnostics.");

    internal static GUIコマンド定義 危険絞り込み { get; } =
        new(
            "danger-filter",
            null,
            null,
            [Key.V, Key.D],
            "Danger診断の表示を切り替えます。",
            "Toggle Danger diagnostics."
        );

    internal static GUIコマンド定義 警告絞り込み { get; } =
        new(
            "warning-filter",
            null,
            null,
            [Key.V, Key.W],
            "Warning診断の表示を切り替えます。",
            "Toggle Warning diagnostics."
        );

    internal static GUIコマンド定義 注意絞り込み { get; } =
        new(
            "attention-filter",
            null,
            null,
            [Key.V, Key.A],
            "Attention診断の表示を切り替えます。",
            "Toggle Attention diagnostics."
        );

    internal static GUIコマンド定義 言語メニュー { get; } =
        new(
            "language-menu",
            null,
            null,
            [Key.V, Key.L],
            "表示言語メニューを開きます。",
            "Open the UI language menu."
        );

    internal static GUIコマンド定義 テーマメニュー { get; } =
        new(
            "theme-menu",
            null,
            null,
            [Key.V, Key.T],
            "テーマメニューを開きます。",
            "Open the theme menu."
        );

    internal static GUIコマンド定義 日本語表示 { get; } =
        new(
            "language-ja",
            null,
            null,
            [Key.V, Key.L, Key.J],
            "表示言語を日本語へ切り替えます。",
            "Switch the UI language to Japanese."
        );

    internal static GUIコマンド定義 英語表示 { get; } =
        new(
            "language-en",
            null,
            null,
            [Key.V, Key.L, Key.E],
            "表示言語を英語へ切り替えます。",
            "Switch the UI language to English."
        );

    internal static GUIコマンド定義 ダークテーマ { get; } =
        new(
            "theme-dark",
            null,
            null,
            [Key.V, Key.T, Key.D],
            "ダークテーマへ切り替えます。",
            "Switch to the Dark theme."
        );

    internal static GUIコマンド定義 ライトテーマ { get; } =
        new(
            "theme-light",
            null,
            null,
            [Key.V, Key.T, Key.L],
            "ライトテーマへ切り替えます。",
            "Switch to the Light theme."
        );
}
