using System.Collections;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using SkiaSharp;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

public sealed partial class 画面視覚回帰テスト
{
    private const string VisualRegressionEnvironmentVariable =
        "OOP_DESIGN_CHECKER_UI_VISUAL_REGRESSION";
    private const double MaximumChangedPixelRatio = 0.005;
    private const byte PixelDeltaThreshold = 12;
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 対応サイズで日本語の準備画面を描画する()
    {
        準備画面を撮影する("ready-ja-small", 900, 600);
        準備画面を撮影する("ready-ja-default", 1280, 820);
        準備画面を撮影する("ready-ja-large", 1600, 1000);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 英語画面を欠けずに描画する()
    {
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            ウィンドウ.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();
            メイン画面配置を確認する(ウィンドウ);
            画面を撮影する(ウィンドウ, "ready-en-default", "en");
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void テーマ切り替え後も解析と絞り込み状態を保つ()
    {
        var テストデータルート = テストデータルートを取得する();
        var 結果 = CheckerService.Analyze(テストデータルート);
        var ウィンドウ = ウィンドウを作る(1280, 820);

        try
        {
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);

            ウィンドウ.テスト用テーマを設定する(ThemeVariant.Light);
            画面更新を完了する();
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Light, ウィンドウ.ActualThemeVariant);
            Assert.Equal(1, ウィンドウ.テスト用画面.テーマ選択.SelectedIndex);
            画面を撮影する(ウィンドウ, "ready-light-default", "ja");

            ウィンドウ.テスト用解析結果を表示する(結果);
            ウィンドウ.テスト用画面.危険絞り込み.IsChecked = false;
            ウィンドウ.テスト用画面.警告絞り込み.IsChecked = true;
            ウィンドウ.テスト用画面.注意絞り込み.IsChecked = false;
            ウィンドウ.テスト用画面.検索絞り込み.Text = "OOP106";
            ウィンドウ.テスト用絞り込みを適用する();
            画面更新を完了する();

            var 行一覧 = 表示行を取得する(ウィンドウ);
            Assert.NotEmpty(行一覧);
            Assert.All(行一覧, 行 => Assert.Equal("OOP106", 行.RuleId));
            Assert.Equal("OOP106", ウィンドウ.テスト用画面.検索絞り込み.Text);
            集計を照合する(ウィンドウ, 結果.Diagnostics);
            画面を撮影する(ウィンドウ, "fixture-light-default", "ja");

            ウィンドウ.テスト用テーマを設定する(ThemeVariant.Dark);
            画面更新を完了する();
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal("OOP106", ウィンドウ.テスト用画面.検索絞り込み.Text);
            Assert.NotEmpty(表示行を取得する(ウィンドウ));
        }
        finally
        {
            ウィンドウ.テスト用テーマを設定する(ThemeVariant.Dark);
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 両言語でAltアクセスキーからメニューを開く()
    {
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            ウィンドウ.テスト用画面.対象パス欄.Focus();
            キーを押す(ウィンドウ, Key.F, PhysicalKey.F, RawInputModifiers.Alt, "f");
            Assert.True(ウィンドウ.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);
            画面を撮影する(ウィンドウ, "menu-file-open-ja", "ja");
            キーを押す(ウィンドウ, Key.Escape, PhysicalKey.Escape);
            Assert.False(ウィンドウ.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);

            キーを押す(ウィンドウ, Key.A, PhysicalKey.A, RawInputModifiers.Alt, "a");
            Assert.True(ウィンドウ.テスト用画面.メインメニュー部品.解析メニュー.IsSubMenuOpen);
            キーを押す(ウィンドウ, Key.Escape, PhysicalKey.Escape);

            ウィンドウ.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            ウィンドウ.テスト用画面.検索絞り込み.Focus();
            キーを押す(ウィンドウ, Key.V, PhysicalKey.V, RawInputModifiers.Alt, "v");
            Assert.True(ウィンドウ.テスト用画面.メインメニュー部品.表示メニュー.IsSubMenuOpen);
            キーを押す(ウィンドウ, Key.Escape, PhysicalKey.Escape);
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 直接フォーカス操作で入力を保ち通常入力から操作を起動しない()
    {
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            ウィンドウ.テスト用画面.対象パス欄.Text = "target-value";
            ウィンドウ.テスト用画面.設定パス欄.Text = "config-value";
            ウィンドウ.テスト用画面.検索絞り込み.Text = "search-value";

            ウィンドウ.テスト用画面.対象パス欄.Focus();
            ウィンドウ.KeyTextInput("f");
            画面更新を完了する();
            var 入力した対象 = ウィンドウ.テスト用画面.対象パス欄.Text ?? string.Empty;
            Assert.Equal("target-value".Length + 1, 入力した対象.Length);
            Assert.Equal(
                "target-value",
                入力した対象.Replace("f", string.Empty, StringComparison.Ordinal)
            );
            Assert.False(ウィンドウ.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);

            ウィンドウ.テスト用画面.検索絞り込み.Focus();
            キーを押す(ウィンドウ, Key.LeftAlt, PhysicalKey.AltLeft);
            Assert.False(ウィンドウ.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);

            キーを押す(ウィンドウ, Key.T, PhysicalKey.T, RawInputModifiers.Alt, "t");
            Assert.Same(
                ウィンドウ.テスト用画面.対象パス欄,
                ウィンドウ.FocusManager?.GetFocusedElement()
            );
            Assert.Equal(入力した対象, ウィンドウ.テスト用画面.対象パス欄.Text);

            キーを押す(ウィンドウ, Key.C, PhysicalKey.C, RawInputModifiers.Alt, "c");
            Assert.Same(
                ウィンドウ.テスト用画面.設定パス欄,
                ウィンドウ.FocusManager?.GetFocusedElement()
            );
            Assert.Equal("config-value", ウィンドウ.テスト用画面.設定パス欄.Text);

            キーを押す(ウィンドウ, Key.F, PhysicalKey.F, RawInputModifiers.Control, "f");
            Assert.Same(
                ウィンドウ.テスト用画面.検索絞り込み,
                ウィンドウ.FocusManager?.GetFocusedElement()
            );
            Assert.Equal("search-value", ウィンドウ.テスト用画面.検索絞り込み.Text);
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Escapeで編集を抜けてから解析をキャンセルする()
    {
        const string 遅延変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";
        var 変更前の遅延 = Environment.GetEnvironmentVariable(遅延変数名);
        Environment.SetEnvironmentVariable(遅延変数名, "30000");

        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            ウィンドウ.テスト用画面.検索絞り込み.Text = "OOP106";
            ウィンドウ.テスト用画面.検索絞り込み.Focus();
            ウィンドウ.テスト用画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            画面更新を完了する();

            Assert.True(ウィンドウ.テスト用画面.キャンセルボタン.IsEnabled);
            Assert.Same(
                ウィンドウ.テスト用画面.検索絞り込み,
                ウィンドウ.FocusManager?.GetFocusedElement()
            );

            キーを押す(ウィンドウ, Key.Escape, PhysicalKey.Escape);
            Assert.Equal("OOP106", ウィンドウ.テスト用画面.検索絞り込み.Text);
            Assert.NotSame(
                ウィンドウ.テスト用画面.検索絞り込み,
                ウィンドウ.FocusManager?.GetFocusedElement()
            );
            Assert.True(ウィンドウ.テスト用画面.キャンセルボタン.IsEnabled);

            キーを押す(ウィンドウ, Key.Escape, PhysicalKey.Escape);
            Assert.False(ウィンドウ.テスト用画面.キャンセルボタン.IsEnabled);
            条件成立まで待つ(
                () =>
                    (ウィンドウ.テスト用画面.状態表示.Text ?? string.Empty).Contains(
                        "キャンセル",
                        StringComparison.OrdinalIgnoreCase
                    ),
                タイムアウト時間: 3000
            );
        }
        finally
        {
            ウィンドウ.Close();
            Environment.SetEnvironmentVariable(遅延変数名, 変更前の遅延);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 両言語のショートカット表示をコマンド定義と一致させる()
    {
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            var 画面 = ウィンドウ.テスト用画面;
            Assert.Contains(
                GUIコマンド.解析開始.主操作の案内,
                画面.ショートカット案内.Text,
                StringComparison.Ordinal
            );
            Assert.Contains(
                GUIコマンド.解析キャンセル.主操作の案内,
                画面.ショートカット案内.Text,
                StringComparison.Ordinal
            );
            Assert.Contains(
                GUIコマンド.診断検索.主操作の案内,
                画面.ショートカット案内.Text,
                StringComparison.Ordinal
            );

            コマンドヘルプを確認する(
                画面.解析ボタン,
                GUIコマンド.解析開始,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                画面.キャンセルボタン,
                GUIコマンド.解析キャンセル,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                画面.検索絞り込み,
                GUIコマンド.診断検索,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                画面.対象パス欄,
                GUIコマンド.対象欄フォーカス,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                画面.設定パス欄,
                GUIコマンド.設定欄フォーカス,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                画面.対象ファイルボタン,
                GUIコマンド.対象ファイル選択,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                画面.JSON出力ボタン,
                GUIコマンド.JSON出力,
                UserInterfaceLanguage.Japanese
            );

            Assert.False(画面.キャンセルボタン.IsEnabled);
            Assert.Contains(
                GUIコマンド.解析キャンセル.ショートカット表示,
                AutomationProperties.GetHelpText(画面.キャンセルボタン),
                StringComparison.Ordinal
            );

            入力操作を照合する(
                画面.メインメニュー部品.解析項目,
                GUIコマンド.解析開始.主ショートカット!.Value
            );
            入力操作を照合する(
                画面.メインメニュー部品.キャンセル項目,
                GUIコマンド.解析キャンセル.主ショートカット!.Value
            );
            入力操作を照合する(
                画面.メインメニュー部品.検索項目,
                GUIコマンド.診断検索.主ショートカット!.Value
            );

            var 日本語のヘルプ = AutomationProperties.GetHelpText(画面.解析ボタン);
            ウィンドウ.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();

            Assert.Contains(
                GUIコマンド.解析開始.主操作の案内,
                画面.ショートカット案内.Text,
                StringComparison.Ordinal
            );
            Assert.Contains(
                GUIコマンド.対象欄フォーカス.主操作の案内,
                AutomationProperties.GetHelpText(画面.対象パス欄),
                StringComparison.Ordinal
            );
            コマンドヘルプを確認する(
                画面.解析ボタン,
                GUIコマンド.解析開始,
                UserInterfaceLanguage.English
            );
            var 英語のヘルプ = AutomationProperties.GetHelpText(画面.解析ボタン);
            Assert.NotEqual(日本語のヘルプ, 英語のヘルプ);
            Assert.Contains("Start analysis.", 英語のヘルプ, StringComparison.Ordinal);
            Assert.Contains(
                GUIコマンド.解析開始.ショートカット表示,
                英語のヘルプ,
                StringComparison.Ordinal
            );
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void メニュー状態を絞り込みと言語とテーマと結果有無へ同期する()
    {
        var テストデータルート = テストデータルートを取得する();
        var 結果 = CheckerService.Analyze(テストデータルート);
        var ウィンドウ = ウィンドウを作る(1280, 820);

        try
        {
            var メニュー = ウィンドウ.テスト用画面.メインメニュー部品;
            Assert.True(メニュー.解析項目.IsEnabled);
            Assert.False(メニュー.キャンセル項目.IsEnabled);
            Assert.False(メニュー.JSON出力項目.IsEnabled);
            Assert.False(メニュー.ソースを開く項目.IsEnabled);
            Assert.True(メニュー.危険絞り込み項目.IsChecked);
            Assert.True(メニュー.警告絞り込み項目.IsChecked);
            Assert.True(メニュー.注意絞り込み項目.IsChecked);

            メニュー.警告絞り込み項目.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            画面更新を完了する();
            Assert.False(ウィンドウ.テスト用画面.警告絞り込み.IsChecked);
            Assert.False(メニュー.警告絞り込み項目.IsChecked);

            メニュー.ライトテーマ項目.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            画面更新を完了する();
            Assert.Equal(1, ウィンドウ.テスト用画面.テーマ選択.SelectedIndex);
            Assert.True(メニュー.ライトテーマ項目.IsChecked);
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);

            メニュー.英語項目.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            画面更新を完了する();
            Assert.Equal(1, ウィンドウ.テスト用画面.表示言語選択.SelectedIndex);
            Assert.True(メニュー.英語項目.IsChecked);

            ウィンドウ.テスト用解析結果を表示する(結果);
            画面更新を完了する();
            Assert.True(メニュー.JSON出力項目.IsEnabled);

            var 行 = 表示行を取得する(ウィンドウ).First();
            ウィンドウ.テスト用画面.診断一覧.SelectedItem = 行;
            ウィンドウ.テスト用選択診断を更新する();
            画面更新を完了する();
            Assert.True(メニュー.ソースを開く項目.IsEnabled);
        }
        finally
        {
            ウィンドウ.テスト用テーマを設定する(ThemeVariant.Dark);
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void リリース用fixtureの行と絞り込みと詳細を描画する()
    {
        var テストデータルート = テストデータルートを取得する();
        var 期待する件数 = 期待する診断件数を読む(テストデータルート);
        var 結果 = CheckerService.Analyze(テストデータルート);

        Assert.Equal(期待する件数, 結果.Diagnostics.Count);

        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            ウィンドウ.テスト用解析結果を表示する(結果);
            画面更新を完了する();

            var 行一覧 = 表示行を取得する(ウィンドウ);
            Assert.Equal(期待する件数, 行一覧.Length);
            集計を照合する(ウィンドウ, 結果.Diagnostics);
            メイン画面配置を確認する(ウィンドウ);
            画面を撮影する(ウィンドウ, "fixture-ja-default", "ja");

            ウィンドウ.テスト用画面.危険絞り込み.IsChecked = false;
            ウィンドウ.テスト用画面.警告絞り込み.IsChecked = true;
            ウィンドウ.テスト用画面.注意絞り込み.IsChecked = false;
            ウィンドウ.テスト用画面.検索絞り込み.Text = "OOP106";
            ウィンドウ.テスト用絞り込みを適用する();
            画面更新を完了する();

            行一覧 = 表示行を取得する(ウィンドウ);
            Assert.NotEmpty(行一覧);
            Assert.All(行一覧, 行 => Assert.Equal("OOP106", 行.RuleId));

            ウィンドウ.テスト用画面.診断一覧.SelectedItem = 行一覧[0];
            ウィンドウ.テスト用選択診断を更新する();
            画面更新を完了する();
            Assert.Contains(
                "OOP106",
                ウィンドウ.テスト用画面.詳細規則.Text,
                StringComparison.Ordinal
            );
            画像基準用fixture位置を固定する(ウィンドウ, テストデータルート, 行一覧[0].FilePath);
            画面を撮影する(ウィンドウ, "filter-detail-oop106-ja", "ja");

            ウィンドウ.テスト用画面.危険絞り込み.IsChecked = true;
            ウィンドウ.テスト用画面.警告絞り込み.IsChecked = true;
            ウィンドウ.テスト用画面.注意絞り込み.IsChecked = true;
            ウィンドウ.テスト用画面.検索絞り込み.Text = string.Empty;
            ウィンドウ.テスト用絞り込みを適用する();
            ウィンドウ.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();

            Assert.Equal(期待する件数, 表示行を取得する(ウィンドウ).Length);
            集計を照合する(ウィンドウ, 結果.Diagnostics);
            メイン画面配置を確認する(ウィンドウ);
            画面を撮影する(ウィンドウ, "fixture-en-default", "en");
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void エラーと設定編集画面を描画する()
    {
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            const string エラー = "Release UI validation error";
            ウィンドウ.テスト用エラーを表示する(エラー);
            画面更新を完了する();

            Assert.Equal(エラー, ウィンドウ.テスト用画面.状態表示.Text);
            Assert.Empty(表示行を取得する(ウィンドウ));
            メイン画面配置を確認する(ウィンドウ);
            画面を撮影する(ウィンドウ, "error-ja-default", "ja");
        }
        finally
        {
            ウィンドウ.Close();
        }

        var 編集画面 = new 設定編集画面(
            """
            {
              "disabledRules": ["OOP103"],
              "failureThreshold": "warning"
            }
            """,
            UserInterfaceLanguage.Japanese
        );
        編集画面.Show();
        try
        {
            画面更新を完了する();
            Assert.True(編集画面.ClientSize.Width >= 560);
            Assert.True(編集画面.ClientSize.Height >= 420);
            画面を撮影する(編集画面, "config-editor-ja-default", "ja");
        }
        finally
        {
            編集画面.Close();
        }
    }

    private static void 準備画面を撮影する(string シナリオ, double 幅, double 高さ)
    {
        var ウィンドウ = ウィンドウを作る(幅, 高さ);
        try
        {
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Dark, ウィンドウ.ActualThemeVariant);
            Assert.Equal(0, ウィンドウ.テスト用画面.テーマ選択.SelectedIndex);
            メイン画面配置を確認する(ウィンドウ);
            画面を撮影する(ウィンドウ, シナリオ, "ja");
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    private static void コマンドヘルプを確認する(
        Control コントロール,
        GUIコマンド定義 コマンド,
        UserInterfaceLanguage 言語
    )
    {
        var 期待値 = コマンド.ヘルプ文(言語);
        Assert.Equal(期待値, AutomationProperties.GetHelpText(コントロール));
        Assert.Equal(期待値, ToolTip.GetTip(コントロール)?.ToString());
    }

    private static void 入力操作を照合する(MenuItem 項目, GUIショートカット 期待値)
    {
        var ジェスチャー = Assert.IsType<KeyGesture>(項目.InputGesture);
        Assert.Equal(期待値.Key, ジェスチャー.Key);
        Assert.Equal(期待値.Modifiers, ジェスチャー.KeyModifiers);
    }

    private static void キーを押す(
        Window ウィンドウ,
        Key キー,
        PhysicalKey 物理キー,
        RawInputModifiers 修飾キー = RawInputModifiers.None,
        string? キー記号 = null
    )
    {
        ウィンドウ.KeyPress(キー, 修飾キー, 物理キー, キー記号);
        ウィンドウ.KeyRelease(キー, 修飾キー, 物理キー, キー記号);
        画面更新を完了する();
    }

    private static void 条件成立まで待つ(Func<bool> 判定条件, int タイムアウト時間)
    {
        var 期限 = DateTime.UtcNow.AddMilliseconds(タイムアウト時間);
        while (DateTime.UtcNow < 期限)
        {
            画面更新を完了する();
            if (判定条件())
            {
                return;
            }

            Thread.Sleep(10);
        }

        Assert.True(判定条件(), "Timed out waiting for UI state.");
    }

    private static メインウィンドウ ウィンドウを作る(double 幅, double 高さ)
    {
        var ウィンドウ = new メインウィンドウ { Width = 幅, Height = 高さ };
        ウィンドウ.Show();
        画面更新を完了する();
        return ウィンドウ;
    }

    private static void 画面更新を完了する()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static DiagnosticRow[] 表示行を取得する(メインウィンドウ ウィンドウ)
    {
        var ソース = ウィンドウ.テスト用画面.診断一覧.ItemsSource as IEnumerable;
        return ソース?.Cast<object>().OfType<DiagnosticRow>().ToArray() ?? [];
    }

    private static void 集計を照合する(
        メインウィンドウ ウィンドウ,
        IReadOnlyList<DesignDiagnostic> 診断一覧
    )
    {
        var 危険診断 = 診断一覧.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Danger);
        var 警告診断 = 診断一覧.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Warning);
        var 注意診断 = 診断一覧.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Attention);

        Assert.EndsWith(
            $": {危険診断}",
            ウィンドウ.テスト用画面.危険件数表示.Text,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            $": {警告診断}",
            ウィンドウ.テスト用画面.警告件数表示.Text,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            $": {注意診断}",
            ウィンドウ.テスト用画面.注意件数表示.Text,
            StringComparison.Ordinal
        );
    }

    private static void メイン画面配置を確認する(メインウィンドウ ウィンドウ)
    {
        var コントロール一覧 = new Control[]
        {
            ウィンドウ.テスト用画面.メインメニュー部品,
            ウィンドウ.テスト用画面.対象パス欄,
            ウィンドウ.テスト用画面.対象ファイルボタン,
            ウィンドウ.テスト用画面.対象フォルダーボタン,
            ウィンドウ.テスト用画面.設定パス欄,
            ウィンドウ.テスト用画面.設定選択ボタン,
            ウィンドウ.テスト用画面.設定編集ボタン,
            ウィンドウ.テスト用画面.設定消去ボタン,
            ウィンドウ.テスト用画面.危険絞り込み,
            ウィンドウ.テスト用画面.警告絞り込み,
            ウィンドウ.テスト用画面.注意絞り込み,
            ウィンドウ.テスト用画面.検索絞り込み,
            ウィンドウ.テスト用画面.解析ボタン,
            ウィンドウ.テスト用画面.キャンセルボタン,
            ウィンドウ.テスト用画面.表示言語選択,
            ウィンドウ.テスト用画面.テーマ選択,
            ウィンドウ.テスト用画面.診断一覧,
            ウィンドウ.テスト用画面.状態表示,
        };

        foreach (var コントロール in コントロール一覧)
        {
            Assert.True(
                コントロール.Bounds.Width > 0,
                $"{コントロール.GetType().Name} has zero width."
            );
            Assert.True(
                コントロール.Bounds.Height > 0,
                $"{コントロール.GetType().Name} has zero height."
            );

            var 原点 = コントロール.TranslatePoint(new Point(0, 0), ウィンドウ);
            Assert.True(原点.HasValue, $"{コントロール.GetType().Name} could not be located.");
            var 境界 = new Rect(原点!.Value, コントロール.Bounds.Size);

            Assert.True(境界.X >= -1, $"{コントロール.GetType().Name} is clipped on the left.");
            Assert.True(境界.Y >= -1, $"{コントロール.GetType().Name} is clipped at the top.");
            Assert.True(
                境界.Right <= ウィンドウ.ClientSize.Width + 1,
                $"{コントロール.GetType().Name} is clipped on the right."
            );
            Assert.True(
                境界.Bottom <= ウィンドウ.ClientSize.Height + 1,
                $"{コントロール.GetType().Name} is clipped at the bottom."
            );
        }

        コントロールの重なりがないことを確認する(
            ウィンドウ,
            [
                ウィンドウ.テスト用画面.危険絞り込み,
                ウィンドウ.テスト用画面.警告絞り込み,
                ウィンドウ.テスト用画面.注意絞り込み,
                ウィンドウ.テスト用画面.検索絞り込み,
                ウィンドウ.テスト用画面.解析ボタン,
                ウィンドウ.テスト用画面.キャンセルボタン,
                ウィンドウ.テスト用画面.表示言語選択,
                ウィンドウ.テスト用画面.テーマ選択,
            ]
        );
    }

    private static void コントロールの重なりがないことを確認する(
        メインウィンドウ ウィンドウ,
        IReadOnlyList<Control> コントロール一覧
    )
    {
        for (var 左側位置 = 0; 左側位置 < コントロール一覧.Count; 左側位置++)
        {
            var 左側 = 相対境界を取得する(ウィンドウ, コントロール一覧[左側位置]);
            for (var 右側位置 = 左側位置 + 1; 右側位置 < コントロール一覧.Count; 右側位置++)
            {
                var 右側 = 相対境界を取得する(ウィンドウ, コントロール一覧[右側位置]);
                var 交差領域 = 左側.Intersect(右側);
                Assert.True(
                    交差領域.Width <= 1 || 交差領域.Height <= 1,
                    $"{コントロール一覧[左側位置].GetType().Name} overlaps {コントロール一覧[右側位置].GetType().Name}."
                );
            }
        }
    }

    private static Rect 相対境界を取得する(Window ウィンドウ, Control コントロール)
    {
        var 原点 = コントロール.TranslatePoint(new Point(0, 0), ウィンドウ);
        Assert.True(原点.HasValue);
        return new Rect(原点!.Value, コントロール.Bounds.Size);
    }

    private static void 画面を撮影する(Window ウィンドウ, string シナリオ, string 言語)
    {
        画面更新を完了する();

        if (!画像回帰確認が要求されている())
        {
            return;
        }

        標準画像環境を確認する();

        var フレーム = ウィンドウ.CaptureRenderedFrame();
        Assert.NotNull(フレーム);

        var 基準画像パス = Path.Combine(画像基準ルートを取得する(), $"{シナリオ}.png");
        var 証跡ディレクトリ = Path.Combine(証跡ルートを取得する(), シナリオ);
        Directory.CreateDirectory(証跡ディレクトリ);

        var 実際のパス = Path.Combine(証跡ディレクトリ, "actual.png");
        var 差分画像パス = Path.Combine(証跡ディレクトリ, "diff.png");
        フレーム.Save(実際のパス, PngBitmapEncoderOptions.Default);

        UI状態を書き出す(ウィンドウ, シナリオ, 言語, 証跡ディレクトリ);

        if (Environment.GetEnvironmentVariable("UPDATE_VISUAL_BASELINES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(基準画像パス)!);
            File.Copy(実際のパス, 基準画像パス, overwrite: true);
            return;
        }

        Assert.True(File.Exists(基準画像パス), $"Missing visual baseline: {基準画像パス}");
        var 変更率 = 差分画像を作る(基準画像パス, 実際のパス, 差分画像パス);
        Assert.True(
            変更率 <= MaximumChangedPixelRatio,
            $"Visual regression for {シナリオ}: {変更率:P3} pixels changed; "
                + $"threshold is {MaximumChangedPixelRatio:P3}. Diff: {差分画像パス}"
        );
    }

    private static void 画像基準用fixture位置を固定する(
        メインウィンドウ ウィンドウ,
        string テストデータルート,
        string ソースパス
    )
    {
        if (!画像回帰確認が要求されている())
        {
            return;
        }

        // fixtureの保存先ではなく画面レイアウトを画像で検証し、移行による誤検出を防ぐ。
        var リポジトリルート = Path.GetFullPath(
            Path.Combine(リポジトリルートを取得する(), "..", "..")
        );
        var 相対パス = Path.GetRelativePath(テストデータルート, ソースパス);
        var 安定化したパス = Path.Combine(
            リポジトリルート,
            "tests",
            "fixtures",
            "release-validation-csharp",
            相対パス
        );
        ウィンドウ.テスト用画面.詳細位置.Text = (
            ウィンドウ.テスト用画面.詳細位置.Text ?? string.Empty
        ).Replace(ソースパス, 安定化したパス, StringComparison.Ordinal);
    }

    private static bool 画像回帰確認が要求されている() =>
        Environment.GetEnvironmentVariable(VisualRegressionEnvironmentVariable) == "1"
        || Environment.GetEnvironmentVariable("UPDATE_VISUAL_BASELINES") == "1";

    private static void 標準画像環境を確認する()
    {
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        throw new InvalidOperationException(
            "Pixel visual regression is pinned to the canonical Linux/Skia environment. "
                + $"Run without {VisualRegressionEnvironmentVariable}=1 on Windows/macOS "
                + "to execute the platform-independent structural UI assertions only."
        );
    }

    private static double 差分画像を作る(
        string 期待するパス,
        string 実際のパス,
        string 差分画像パス
    )
    {
        using var 期待値 = SKBitmap.Decode(期待するパス);
        using var 実際の値 = SKBitmap.Decode(実際のパス);

        Assert.NotNull(期待値);
        Assert.NotNull(実際の値);
        Assert.Equal(期待値.Width, 実際の値.Width);
        Assert.Equal(期待値.Height, 実際の値.Height);

        using var 差分 = new SKBitmap(
            実際の値.Width,
            実際の値.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );

        long 変更画素数 = 0;
        var 総画素数 = (long)実際の値.Width * 実際の値.Height;
        for (var 縦位置 = 0; 縦位置 < 実際の値.Height; 縦位置++)
        {
            for (var 横位置 = 0; 横位置 < 実際の値.Width; 横位置++)
            {
                var 期待する画素数 = 期待値.GetPixel(横位置, 縦位置);
                var 実際の画素数 = 実際の値.GetPixel(横位置, 縦位置);
                var 差分量 = Math.Max(
                    Math.Max(
                        Math.Abs(期待する画素数.Red - 実際の画素数.Red),
                        Math.Abs(期待する画素数.Green - 実際の画素数.Green)
                    ),
                    Math.Max(
                        Math.Abs(期待する画素数.Blue - 実際の画素数.Blue),
                        Math.Abs(期待する画素数.Alpha - 実際の画素数.Alpha)
                    )
                );

                if (差分量 > PixelDeltaThreshold)
                {
                    変更画素数++;
                    差分.SetPixel(横位置, 縦位置, SKColors.Red);
                }
                else
                {
                    差分.SetPixel(
                        横位置,
                        縦位置,
                        new SKColor(実際の画素数.Red, 実際の画素数.Green, 実際の画素数.Blue, 64)
                    );
                }
            }
        }

        using var 画像 = SKImage.FromBitmap(差分);
        using var データ = 画像.Encode(SKEncodedImageFormat.Png, 100);
        using var ストリーム = File.Create(差分画像パス);
        データ.SaveTo(ストリーム);

        return 総画素数 == 0 ? 0 : (double)変更画素数 / 総画素数;
    }

    private static void UI状態を書き出す(
        Window ウィンドウ,
        string シナリオ,
        string 言語,
        string 証跡ディレクトリ
    )
    {
        var 状態 = new
        {
            シナリオ,
            言語,
            幅 = ウィンドウ.ClientSize.Width,
            高さ = ウィンドウ.ClientSize.Height,
            タイトル = ウィンドウ.Title,
            テーマ = ウィンドウ.ActualThemeVariant.ToString(),
            表示行数 = ウィンドウ is メインウィンドウ 主処理
                ? 表示行を取得する(主処理).Length
                : (int?)null,
            状態 = ウィンドウ is メインウィンドウ 状態ウィンドウ
                ? 状態ウィンドウ.テスト用画面.状態表示.Text
                : null,
            選択規則 = ウィンドウ is メインウィンドウ 選択ウィンドウ
            && 選択ウィンドウ.テスト用画面.診断一覧.SelectedItem is DiagnosticRow 行
                ? 行.RuleId
                : null,
            選択ファイル = ウィンドウ is メインウィンドウ 選択ファイルウィンドウ
            && 選択ファイルウィンドウ.テスト用画面.診断一覧.SelectedItem is DiagnosticRow ファイル行
                ? ファイル行.FileName
                : null,
            概要 = ウィンドウ is メインウィンドウ 概要ウィンドウ
                ? new
                {
                    危険診断 = 概要ウィンドウ.テスト用画面.危険件数表示.Text,
                    警告診断 = 概要ウィンドウ.テスト用画面.警告件数表示.Text,
                    注意診断 = 概要ウィンドウ.テスト用画面.注意件数表示.Text,
                    設定 = 概要ウィンドウ.テスト用画面.設定概要.Text,
                }
                : null,
        };

        File.WriteAllText(
            Path.Combine(証跡ディレクトリ, "ui-state.json"),
            JsonSerializer.Serialize(状態, IndentedJson)
        );
    }

    private static int 期待する診断件数を読む(string テストデータルート)
    {
        using var 文書 = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(テストデータルート, "expected-diagnostics.json"))
        );
        return 文書.RootElement.GetProperty("exactDiagnostics").GetArrayLength();
    }

    private static string テストデータルートを取得する() =>
        Path.Combine(
            リポジトリルートを取得する(),
            "tests",
            "fixtures",
            "release-validation-csharp"
        );

    private static string 画像基準ルートを取得する() =>
        Path.Combine(
            リポジトリルートを取得する(),
            "tests",
            "OopDesignChecker.Gui.HeadlessTests",
            "Baselines"
        );

    private static string 証跡ルートを取得する()
    {
        var 設定済み = Environment.GetEnvironmentVariable("UI_EVIDENCE_DIR");
        return string.IsNullOrWhiteSpace(設定済み)
            ? Path.Combine(Path.GetTempPath(), "oop-design-checker-ui-evidence")
            : 設定済み;
    }

    private static string リポジトリルートを取得する()
    {
        foreach (var 候補 in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var 現在値 = new DirectoryInfo(候補);
            while (現在値 is not null)
            {
                if (File.Exists(Path.Combine(現在値.FullName, "OopDesignChecker.sln")))
                {
                    return 現在値.FullName;
                }

                現在値 = 現在値.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
