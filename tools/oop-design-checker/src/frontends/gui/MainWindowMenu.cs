using Avalonia.Automation;
using Avalonia.Controls;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Gui;

internal sealed class メインメニュー : Menu
{
    protected override Type StyleKeyOverride => typeof(Menu);

    internal MenuItem ファイルメニュー { get; } = new();
    internal MenuItem 解析メニュー { get; } = new();
    internal MenuItem 表示メニュー { get; } = new();

    internal MenuItem 対象ファイル項目 { get; } = new();
    internal MenuItem 対象フォルダー項目 { get; } = new();
    internal MenuItem 設定項目 { get; } = new();
    internal MenuItem 設定編集項目 { get; } = new();
    internal MenuItem 設定消去項目 { get; } = new();
    internal MenuItem JSON出力項目 { get; } = new();
    internal MenuItem SARIF出力項目 { get; } = new();
    internal MenuItem 終了項目 { get; } = new();

    internal MenuItem 解析項目 { get; } = new();
    internal MenuItem キャンセル項目 { get; } = new();
    internal MenuItem ソースを開く項目 { get; } = new();

    internal MenuItem 検索項目 { get; } = new();
    internal MenuItem 危険絞り込み項目 { get; } = new();
    internal MenuItem 警告絞り込み項目 { get; } = new();
    internal MenuItem 注意絞り込み項目 { get; } = new();
    internal MenuItem 言語メニュー { get; } = new();
    internal MenuItem 日本語項目 { get; } = new();
    internal MenuItem 英語項目 { get; } = new();
    internal MenuItem テーマメニュー { get; } = new();
    internal MenuItem ダークテーマ項目 { get; } = new();
    internal MenuItem ライトテーマ項目 { get; } = new();

    public メインメニュー()
    {
        MinHeight = 28;

        項目を追加する(
            ファイルメニュー,
            対象ファイル項目,
            対象フォルダー項目,
            new Separator(),
            設定項目,
            設定編集項目,
            設定消去項目,
            new Separator(),
            JSON出力項目,
            SARIF出力項目,
            new Separator(),
            終了項目
        );

        項目を追加する(解析メニュー, 解析項目, キャンセル項目, new Separator(), ソースを開く項目);

        項目を追加する(言語メニュー, 日本語項目, 英語項目);
        項目を追加する(テーマメニュー, ダークテーマ項目, ライトテーマ項目);
        項目を追加する(
            表示メニュー,
            検索項目,
            new Separator(),
            危険絞り込み項目,
            警告絞り込み項目,
            注意絞り込み項目,
            new Separator(),
            言語メニュー,
            テーマメニュー
        );

        項目を追加する(this, ファイルメニュー, 解析メニュー, 表示メニュー);

        危険絞り込み項目.ToggleType = MenuItemToggleType.CheckBox;
        警告絞り込み項目.ToggleType = MenuItemToggleType.CheckBox;
        注意絞り込み項目.ToggleType = MenuItemToggleType.CheckBox;

        日本語項目.ToggleType = MenuItemToggleType.Radio;
        日本語項目.GroupName = "Language";
        英語項目.ToggleType = MenuItemToggleType.Radio;
        英語項目.GroupName = "Language";

        ダークテーマ項目.ToggleType = MenuItemToggleType.Radio;
        ダークテーマ項目.GroupName = "Theme";
        ライトテーマ項目.ToggleType = MenuItemToggleType.Radio;
        ライトテーマ項目.GroupName = "Theme";

        解析項目.InputGesture = GUIコマンド.解析開始.主ショートカット?.キー操作に変換する();
        キャンセル項目.InputGesture =
            GUIコマンド.解析キャンセル.主ショートカット?.キー操作に変換する();
        検索項目.InputGesture = GUIコマンド.診断検索.主ショートカット?.キー操作に変換する();

        自動化設定を適用する();
        表示言語を適用する(UserInterfaceLanguage.Japanese);
    }

    internal void 表示言語を適用する(UserInterfaceLanguage language)
    {
        最上位メニュー見出しを設定する(
            ファイルメニュー,
            GUI文言.取得する(GUI文言キー.ファイルメニュー, language),
            GUIコマンド.ファイルメニュー,
            language
        );
        最上位メニュー見出しを設定する(
            解析メニュー,
            GUI文言.取得する(GUI文言キー.解析メニュー, language),
            GUIコマンド.解析メニュー,
            language
        );
        最上位メニュー見出しを設定する(
            表示メニュー,
            GUI文言.取得する(GUI文言キー.表示メニュー, language),
            GUIコマンド.表示メニュー,
            language
        );

        項目見出しを設定する(
            対象ファイル項目,
            GUI文言.取得する(GUI文言キー.ファイルボタン, language),
            GUIコマンド.対象ファイル選択,
            language
        );
        項目見出しを設定する(
            対象フォルダー項目,
            GUI文言.取得する(GUI文言キー.フォルダーボタン, language),
            GUIコマンド.対象フォルダー選択,
            language
        );
        項目見出しを設定する(
            設定項目,
            GUI文言.取得する(GUI文言キー.設定ボタン文言, language),
            GUIコマンド.設定選択,
            language
        );
        項目見出しを設定する(
            設定編集項目,
            GUI文言.取得する(GUI文言キー.編集ボタン, language),
            GUIコマンド.設定編集,
            language
        );
        項目見出しを設定する(
            設定消去項目,
            GUI文言.取得する(GUI文言キー.消去ボタン, language),
            GUIコマンド.設定消去,
            language
        );
        項目見出しを設定する(
            JSON出力項目,
            GUI文言.取得する(GUI文言キー.JSON出力ボタン, language),
            GUIコマンド.JSON出力,
            language
        );
        項目見出しを設定する(
            SARIF出力項目,
            GUI文言.取得する(GUI文言キー.SARIF出力ボタン, language),
            GUIコマンド.SARIF出力,
            language
        );
        項目見出しを設定する(
            終了項目,
            GUI文言.取得する(GUI文言キー.終了メニュー, language),
            GUIコマンド.終了,
            language
        );

        項目見出しを設定する(
            解析項目,
            GUI文言.取得する(GUI文言キー.解析ボタン, language),
            GUIコマンド.解析開始,
            language
        );
        項目見出しを設定する(
            キャンセル項目,
            GUI文言.取得する(GUI文言キー.キャンセルボタン, language),
            GUIコマンド.解析キャンセル,
            language
        );
        項目見出しを設定する(
            ソースを開く項目,
            GUI文言.取得する(GUI文言キー.ソースを開くボタン, language),
            GUIコマンド.ソースを開く,
            language
        );

        項目見出しを設定する(
            検索項目,
            GUI文言.取得する(GUI文言キー.検索メニュー, language),
            GUIコマンド.診断検索,
            language
        );
        項目見出しを設定する(
            危険絞り込み項目,
            GUI文言.取得する(GUI文言キー.危険, language),
            GUIコマンド.危険絞り込み,
            language
        );
        項目見出しを設定する(
            警告絞り込み項目,
            GUI文言.取得する(GUI文言キー.警告, language),
            GUIコマンド.警告絞り込み,
            language
        );
        項目見出しを設定する(
            注意絞り込み項目,
            GUI文言.取得する(GUI文言キー.注意, language),
            GUIコマンド.注意絞り込み,
            language
        );
        項目見出しを設定する(
            言語メニュー,
            GUI文言.取得する(GUI文言キー.言語ラベル, language).TrimEnd(':'),
            GUIコマンド.言語メニュー,
            language
        );
        項目見出しを設定する(
            日本語項目,
            GUI文言.取得する(GUI文言キー.日本語表示, language),
            GUIコマンド.日本語表示,
            language
        );
        項目見出しを設定する(
            英語項目,
            GUI文言.取得する(GUI文言キー.英語表示, language),
            GUIコマンド.英語表示,
            language
        );
        項目見出しを設定する(
            テーマメニュー,
            GUI文言.取得する(GUI文言キー.テーマラベル, language).TrimEnd(':'),
            GUIコマンド.テーマメニュー,
            language
        );
        項目見出しを設定する(
            ダークテーマ項目,
            GUI文言.取得する(GUI文言キー.ダークテーマ, language),
            GUIコマンド.ダークテーマ,
            language
        );
        項目見出しを設定する(
            ライトテーマ項目,
            GUI文言.取得する(GUI文言キー.ライトテーマ, language),
            GUIコマンド.ライトテーマ,
            language
        );
    }

    private static void 項目を追加する(ItemsControl parent, params object[] items)
    {
        foreach (var item in items)
        {
            parent.Items.Add(item);
        }
    }

    private void 自動化設定を適用する()
    {
        var items = new (MenuItem Item, string 識別子)[]
        {
            (ファイルメニュー, "FileMenu"),
            (解析メニュー, "AnalyzeMenu"),
            (表示メニュー, "ViewMenu"),
            (対象ファイル項目, "TargetFileMenuItem"),
            (対象フォルダー項目, "TargetFolderMenuItem"),
            (設定項目, "ConfigurationMenuItem"),
            (設定編集項目, "EditConfigurationMenuItem"),
            (設定消去項目, "ClearConfigurationMenuItem"),
            (JSON出力項目, "ExportJsonMenuItem"),
            (SARIF出力項目, "ExportSarifMenuItem"),
            (終了項目, "ExitMenuItem"),
            (解析項目, "AnalyzeMenuItem"),
            (キャンセル項目, "CancelMenuItem"),
            (ソースを開く項目, "OpenSourceMenuItem"),
            (検索項目, "SearchMenuItem"),
            (危険絞り込み項目, "DangerFilterMenuItem"),
            (警告絞り込み項目, "WarningFilterMenuItem"),
            (注意絞り込み項目, "AttentionFilterMenuItem"),
            (言語メニュー, "LanguageMenu"),
            (日本語項目, "JapaneseLanguageMenuItem"),
            (英語項目, "EnglishLanguageMenuItem"),
            (テーマメニュー, "ThemeMenu"),
            (ダークテーマ項目, "DarkThemeMenuItem"),
            (ライトテーマ項目, "LightThemeMenuItem"),
        };

        AutomationProperties.SetAutomationId(this, "MainMenu");
        foreach (var (item, id) in items)
        {
            AutomationProperties.SetAutomationId(item, id);
        }
    }

    private static void 最上位メニュー見出しを設定する(
        MenuItem item,
        string text,
        GUIコマンド定義 command,
        UserInterfaceLanguage language
    )
    {
        var accessKey = アクセスキーを取得する(command);
        var header =
            language == UserInterfaceLanguage.English
                ? アクセスキー付き見出しを作る(text, accessKey)
                : $"{text} (_{accessKey})";
        item.Header = header;
        AutomationProperties.SetName(item, text);
        command.ヘルプを適用する(item, language);
    }

    private static void 項目見出しを設定する(
        MenuItem item,
        string text,
        GUIコマンド定義 command,
        UserInterfaceLanguage language
    )
    {
        var accessKey = アクセスキーを取得する(command);
        item.Header = $"{text} (_{accessKey})";
        AutomationProperties.SetName(item, text);
        command.ヘルプを適用する(item, language);
    }

    private static string アクセスキーを取得する(GUIコマンド定義 command) =>
        command.メニューアクセスキー is { } key
            ? GUIショートカット.キー名を整形する(key)
            : throw new InvalidOperationException(
                $"Command {command.識別子} has no menu access key."
            );

    private static string アクセスキー付き見出しを作る(string text, string accessKey)
    {
        var index = text.IndexOf(accessKey, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? $"{text} (_{accessKey})" : text.Insert(index, "_");
    }
}
