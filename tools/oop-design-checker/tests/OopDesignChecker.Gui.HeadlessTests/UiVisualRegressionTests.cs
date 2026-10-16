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

public sealed class 画面視覚回帰テスト
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
        var window = ウィンドウを作る(1280, 820);
        try
        {
            window.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();
            メイン画面配置を確認する(window);
            画面を撮影する(window, "ready-en-default", "en");
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void テーマ切り替え後も解析と絞り込み状態を保つ()
    {
        var fixtureRoot = fixtureルートを取得する();
        var result = CheckerService.Analyze(fixtureRoot);
        var window = ウィンドウを作る(1280, 820);

        try
        {
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);

            window.テスト用テーマを設定する(ThemeVariant.Light);
            画面更新を完了する();
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(1, window.テスト用画面.テーマ選択.SelectedIndex);
            画面を撮影する(window, "ready-light-default", "ja");

            window.テスト用解析結果を表示する(result);
            window.テスト用画面.危険絞り込み.IsChecked = false;
            window.テスト用画面.警告絞り込み.IsChecked = true;
            window.テスト用画面.注意絞り込み.IsChecked = false;
            window.テスト用画面.検索絞り込み.Text = "OOP106";
            window.テスト用絞り込みを適用する();
            画面更新を完了する();

            var rows = 表示行を取得する(window);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal("OOP106", row.RuleId));
            Assert.Equal("OOP106", window.テスト用画面.検索絞り込み.Text);
            集計を照合する(window, result.Diagnostics);
            画面を撮影する(window, "fixture-light-default", "ja");

            window.テスト用テーマを設定する(ThemeVariant.Dark);
            画面更新を完了する();
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal("OOP106", window.テスト用画面.検索絞り込み.Text);
            Assert.NotEmpty(表示行を取得する(window));
        }
        finally
        {
            window.テスト用テーマを設定する(ThemeVariant.Dark);
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 両言語でAltアクセスキーからメニューを開く()
    {
        var window = ウィンドウを作る(1280, 820);
        try
        {
            window.テスト用画面.対象パス欄.Focus();
            キーを押す(window, Key.F, PhysicalKey.F, RawInputModifiers.Alt, "f");
            Assert.True(window.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);
            画面を撮影する(window, "menu-file-open-ja", "ja");
            キーを押す(window, Key.Escape, PhysicalKey.Escape);
            Assert.False(window.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);

            キーを押す(window, Key.A, PhysicalKey.A, RawInputModifiers.Alt, "a");
            Assert.True(window.テスト用画面.メインメニュー部品.解析メニュー.IsSubMenuOpen);
            キーを押す(window, Key.Escape, PhysicalKey.Escape);

            window.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            window.テスト用画面.検索絞り込み.Focus();
            キーを押す(window, Key.V, PhysicalKey.V, RawInputModifiers.Alt, "v");
            Assert.True(window.テスト用画面.メインメニュー部品.表示メニュー.IsSubMenuOpen);
            キーを押す(window, Key.Escape, PhysicalKey.Escape);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 直接フォーカス操作で入力を保ち通常入力から操作を起動しない()
    {
        var window = ウィンドウを作る(1280, 820);
        try
        {
            window.テスト用画面.対象パス欄.Text = "target-value";
            window.テスト用画面.設定パス欄.Text = "config-value";
            window.テスト用画面.検索絞り込み.Text = "search-value";

            window.テスト用画面.対象パス欄.Focus();
            window.KeyTextInput("f");
            画面更新を完了する();
            var typedTarget = window.テスト用画面.対象パス欄.Text ?? string.Empty;
            Assert.Equal("target-value".Length + 1, typedTarget.Length);
            Assert.Equal(
                "target-value",
                typedTarget.Replace("f", string.Empty, StringComparison.Ordinal)
            );
            Assert.False(window.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);

            window.テスト用画面.検索絞り込み.Focus();
            キーを押す(window, Key.LeftAlt, PhysicalKey.AltLeft);
            Assert.False(window.テスト用画面.メインメニュー部品.ファイルメニュー.IsSubMenuOpen);

            キーを押す(window, Key.T, PhysicalKey.T, RawInputModifiers.Alt, "t");
            Assert.Same(window.テスト用画面.対象パス欄, window.FocusManager?.GetFocusedElement());
            Assert.Equal(typedTarget, window.テスト用画面.対象パス欄.Text);

            キーを押す(window, Key.C, PhysicalKey.C, RawInputModifiers.Alt, "c");
            Assert.Same(window.テスト用画面.設定パス欄, window.FocusManager?.GetFocusedElement());
            Assert.Equal("config-value", window.テスト用画面.設定パス欄.Text);

            キーを押す(window, Key.F, PhysicalKey.F, RawInputModifiers.Control, "f");
            Assert.Same(window.テスト用画面.検索絞り込み, window.FocusManager?.GetFocusedElement());
            Assert.Equal("search-value", window.テスト用画面.検索絞り込み.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Escapeで編集を抜けてから解析をキャンセルする()
    {
        const string delayVariable = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";
        var previousDelay = Environment.GetEnvironmentVariable(delayVariable);
        Environment.SetEnvironmentVariable(delayVariable, "30000");

        var window = ウィンドウを作る(1280, 820);
        try
        {
            window.テスト用画面.検索絞り込み.Text = "OOP106";
            window.テスト用画面.検索絞り込み.Focus();
            window.テスト用画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            画面更新を完了する();

            Assert.True(window.テスト用画面.キャンセルボタン.IsEnabled);
            Assert.Same(window.テスト用画面.検索絞り込み, window.FocusManager?.GetFocusedElement());

            キーを押す(window, Key.Escape, PhysicalKey.Escape);
            Assert.Equal("OOP106", window.テスト用画面.検索絞り込み.Text);
            Assert.NotSame(
                window.テスト用画面.検索絞り込み,
                window.FocusManager?.GetFocusedElement()
            );
            Assert.True(window.テスト用画面.キャンセルボタン.IsEnabled);

            キーを押す(window, Key.Escape, PhysicalKey.Escape);
            Assert.False(window.テスト用画面.キャンセルボタン.IsEnabled);
            条件成立まで待つ(
                () =>
                    (window.テスト用画面.状態表示.Text ?? string.Empty).Contains(
                        "キャンセル",
                        StringComparison.OrdinalIgnoreCase
                    ),
                timeoutMilliseconds: 3000
            );
        }
        finally
        {
            window.Close();
            Environment.SetEnvironmentVariable(delayVariable, previousDelay);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 両言語のショートカット表示をコマンド定義と一致させる()
    {
        var window = ウィンドウを作る(1280, 820);
        try
        {
            var view = window.テスト用画面;
            Assert.Contains(
                GUIコマンド.解析開始.主操作の案内,
                view.ショートカット案内.Text,
                StringComparison.Ordinal
            );
            Assert.Contains(
                GUIコマンド.解析キャンセル.主操作の案内,
                view.ショートカット案内.Text,
                StringComparison.Ordinal
            );
            Assert.Contains(
                GUIコマンド.診断検索.主操作の案内,
                view.ショートカット案内.Text,
                StringComparison.Ordinal
            );

            コマンドヘルプを確認する(
                view.解析ボタン,
                GUIコマンド.解析開始,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                view.キャンセルボタン,
                GUIコマンド.解析キャンセル,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                view.検索絞り込み,
                GUIコマンド.診断検索,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                view.対象パス欄,
                GUIコマンド.対象欄フォーカス,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                view.設定パス欄,
                GUIコマンド.設定欄フォーカス,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                view.対象ファイルボタン,
                GUIコマンド.対象ファイル選択,
                UserInterfaceLanguage.Japanese
            );
            コマンドヘルプを確認する(
                view.JSON出力ボタン,
                GUIコマンド.JSON出力,
                UserInterfaceLanguage.Japanese
            );

            Assert.False(view.キャンセルボタン.IsEnabled);
            Assert.Contains(
                GUIコマンド.解析キャンセル.ショートカット表示,
                AutomationProperties.GetHelpText(view.キャンセルボタン),
                StringComparison.Ordinal
            );

            入力操作を照合する(
                view.メインメニュー部品.解析項目,
                GUIコマンド.解析開始.主ショートカット!.Value
            );
            入力操作を照合する(
                view.メインメニュー部品.キャンセル項目,
                GUIコマンド.解析キャンセル.主ショートカット!.Value
            );
            入力操作を照合する(
                view.メインメニュー部品.検索項目,
                GUIコマンド.診断検索.主ショートカット!.Value
            );

            var japaneseHelp = AutomationProperties.GetHelpText(view.解析ボタン);
            window.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();

            Assert.Contains(
                GUIコマンド.解析開始.主操作の案内,
                view.ショートカット案内.Text,
                StringComparison.Ordinal
            );
            Assert.Contains(
                GUIコマンド.対象欄フォーカス.主操作の案内,
                AutomationProperties.GetHelpText(view.対象パス欄),
                StringComparison.Ordinal
            );
            コマンドヘルプを確認する(
                view.解析ボタン,
                GUIコマンド.解析開始,
                UserInterfaceLanguage.English
            );
            var englishHelp = AutomationProperties.GetHelpText(view.解析ボタン);
            Assert.NotEqual(japaneseHelp, englishHelp);
            Assert.Contains("Start analysis.", englishHelp, StringComparison.Ordinal);
            Assert.Contains(
                GUIコマンド.解析開始.ショートカット表示,
                englishHelp,
                StringComparison.Ordinal
            );
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void メニュー状態を絞り込みと言語とテーマと結果有無へ同期する()
    {
        var fixtureRoot = fixtureルートを取得する();
        var result = CheckerService.Analyze(fixtureRoot);
        var window = ウィンドウを作る(1280, 820);

        try
        {
            var menu = window.テスト用画面.メインメニュー部品;
            Assert.True(menu.解析項目.IsEnabled);
            Assert.False(menu.キャンセル項目.IsEnabled);
            Assert.False(menu.JSON出力項目.IsEnabled);
            Assert.False(menu.ソースを開く項目.IsEnabled);
            Assert.True(menu.危険絞り込み項目.IsChecked);
            Assert.True(menu.警告絞り込み項目.IsChecked);
            Assert.True(menu.注意絞り込み項目.IsChecked);

            menu.警告絞り込み項目.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            画面更新を完了する();
            Assert.False(window.テスト用画面.警告絞り込み.IsChecked);
            Assert.False(menu.警告絞り込み項目.IsChecked);

            menu.ライトテーマ項目.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            画面更新を完了する();
            Assert.Equal(1, window.テスト用画面.テーマ選択.SelectedIndex);
            Assert.True(menu.ライトテーマ項目.IsChecked);
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);

            menu.英語項目.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            画面更新を完了する();
            Assert.Equal(1, window.テスト用画面.表示言語選択.SelectedIndex);
            Assert.True(menu.英語項目.IsChecked);

            window.テスト用解析結果を表示する(result);
            画面更新を完了する();
            Assert.True(menu.JSON出力項目.IsEnabled);

            var row = 表示行を取得する(window).First();
            window.テスト用画面.診断一覧.SelectedItem = row;
            window.テスト用選択診断を更新する();
            画面更新を完了する();
            Assert.True(menu.ソースを開く項目.IsEnabled);
        }
        finally
        {
            window.テスト用テーマを設定する(ThemeVariant.Dark);
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void リリース用fixtureの行と絞り込みと詳細を描画する()
    {
        var fixtureRoot = fixtureルートを取得する();
        var expectedCount = 期待する診断件数を読む(fixtureRoot);
        var result = CheckerService.Analyze(fixtureRoot);

        Assert.Equal(expectedCount, result.Diagnostics.Count);

        var window = ウィンドウを作る(1280, 820);
        try
        {
            window.テスト用解析結果を表示する(result);
            画面更新を完了する();

            var rows = 表示行を取得する(window);
            Assert.Equal(expectedCount, rows.Length);
            集計を照合する(window, result.Diagnostics);
            メイン画面配置を確認する(window);
            画面を撮影する(window, "fixture-ja-default", "ja");

            window.テスト用画面.危険絞り込み.IsChecked = false;
            window.テスト用画面.警告絞り込み.IsChecked = true;
            window.テスト用画面.注意絞り込み.IsChecked = false;
            window.テスト用画面.検索絞り込み.Text = "OOP106";
            window.テスト用絞り込みを適用する();
            画面更新を完了する();

            rows = 表示行を取得する(window);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal("OOP106", row.RuleId));

            window.テスト用画面.診断一覧.SelectedItem = rows[0];
            window.テスト用選択診断を更新する();
            画面更新を完了する();
            Assert.Contains("OOP106", window.テスト用画面.詳細規則.Text, StringComparison.Ordinal);
            画像基準用fixture位置を固定する(window, fixtureRoot, rows[0].FilePath);
            画面を撮影する(window, "filter-detail-oop106-ja", "ja");

            window.テスト用画面.危険絞り込み.IsChecked = true;
            window.テスト用画面.警告絞り込み.IsChecked = true;
            window.テスト用画面.注意絞り込み.IsChecked = true;
            window.テスト用画面.検索絞り込み.Text = string.Empty;
            window.テスト用絞り込みを適用する();
            window.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();

            Assert.Equal(expectedCount, 表示行を取得する(window).Length);
            集計を照合する(window, result.Diagnostics);
            メイン画面配置を確認する(window);
            画面を撮影する(window, "fixture-en-default", "en");
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void エラーと設定編集画面を描画する()
    {
        var window = ウィンドウを作る(1280, 820);
        try
        {
            const string error = "Release UI validation error";
            window.テスト用エラーを表示する(error);
            画面更新を完了する();

            Assert.Equal(error, window.テスト用画面.状態表示.Text);
            Assert.Empty(表示行を取得する(window));
            メイン画面配置を確認する(window);
            画面を撮影する(window, "error-ja-default", "ja");
        }
        finally
        {
            window.Close();
        }

        var editor = new 設定編集画面(
            """
            {
              "disabledRules": ["OOP103"],
              "failureThreshold": "warning"
            }
            """,
            UserInterfaceLanguage.Japanese
        );
        editor.Show();
        try
        {
            画面更新を完了する();
            Assert.True(editor.ClientSize.Width >= 560);
            Assert.True(editor.ClientSize.Height >= 420);
            画面を撮影する(editor, "config-editor-ja-default", "ja");
        }
        finally
        {
            editor.Close();
        }
    }

    private static void 準備画面を撮影する(string scenario, double width, double height)
    {
        var window = ウィンドウを作る(width, height);
        try
        {
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.Equal(0, window.テスト用画面.テーマ選択.SelectedIndex);
            メイン画面配置を確認する(window);
            画面を撮影する(window, scenario, "ja");
        }
        finally
        {
            window.Close();
        }
    }

    private static void コマンドヘルプを確認する(
        Control control,
        GUIコマンド定義 command,
        UserInterfaceLanguage language
    )
    {
        var expected = command.ヘルプ文(language);
        Assert.Equal(expected, AutomationProperties.GetHelpText(control));
        Assert.Equal(expected, ToolTip.GetTip(control)?.ToString());
    }

    private static void 入力操作を照合する(MenuItem item, GUIショートカット expected)
    {
        var gesture = Assert.IsType<KeyGesture>(item.InputGesture);
        Assert.Equal(expected.Key, gesture.Key);
        Assert.Equal(expected.Modifiers, gesture.KeyModifiers);
    }

    private static void キーを押す(
        Window window,
        Key key,
        PhysicalKey physicalKey,
        RawInputModifiers modifiers = RawInputModifiers.None,
        string? keySymbol = null
    )
    {
        window.KeyPress(key, modifiers, physicalKey, keySymbol);
        window.KeyRelease(key, modifiers, physicalKey, keySymbol);
        画面更新を完了する();
    }

    private static void 条件成立まで待つ(Func<bool> predicate, int timeoutMilliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            画面更新を完了する();
            if (predicate())
            {
                return;
            }

            Thread.Sleep(10);
        }

        Assert.True(predicate(), "Timed out waiting for UI state.");
    }

    private static メインウィンドウ ウィンドウを作る(double width, double height)
    {
        var window = new メインウィンドウ { Width = width, Height = height };
        window.Show();
        画面更新を完了する();
        return window;
    }

    private static void 画面更新を完了する()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static DiagnosticRow[] 表示行を取得する(メインウィンドウ window)
    {
        var source = window.テスト用画面.診断一覧.ItemsSource as IEnumerable;
        return source?.Cast<object>().OfType<DiagnosticRow>().ToArray() ?? [];
    }

    private static void 集計を照合する(
        メインウィンドウ window,
        IReadOnlyList<DesignDiagnostic> diagnostics
    )
    {
        var danger = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Danger);
        var warning = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Warning);
        var attention = diagnostics.Count(item =>
            item.Severity == DesignDiagnosticSeverity.Attention
        );

        Assert.EndsWith(
            $": {danger}",
            window.テスト用画面.危険件数表示.Text,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            $": {warning}",
            window.テスト用画面.警告件数表示.Text,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            $": {attention}",
            window.テスト用画面.注意件数表示.Text,
            StringComparison.Ordinal
        );
    }

    private static void メイン画面配置を確認する(メインウィンドウ window)
    {
        var controls = new Control[]
        {
            window.テスト用画面.メインメニュー部品,
            window.テスト用画面.対象パス欄,
            window.テスト用画面.対象ファイルボタン,
            window.テスト用画面.対象フォルダーボタン,
            window.テスト用画面.設定パス欄,
            window.テスト用画面.設定選択ボタン,
            window.テスト用画面.設定編集ボタン,
            window.テスト用画面.設定消去ボタン,
            window.テスト用画面.危険絞り込み,
            window.テスト用画面.警告絞り込み,
            window.テスト用画面.注意絞り込み,
            window.テスト用画面.検索絞り込み,
            window.テスト用画面.解析ボタン,
            window.テスト用画面.キャンセルボタン,
            window.テスト用画面.表示言語選択,
            window.テスト用画面.テーマ選択,
            window.テスト用画面.診断一覧,
            window.テスト用画面.状態表示,
        };

        foreach (var control in controls)
        {
            Assert.True(control.Bounds.Width > 0, $"{control.GetType().Name} has zero width.");
            Assert.True(control.Bounds.Height > 0, $"{control.GetType().Name} has zero height.");

            var origin = control.TranslatePoint(new Point(0, 0), window);
            Assert.True(origin.HasValue, $"{control.GetType().Name} could not be located.");
            var bounds = new Rect(origin!.Value, control.Bounds.Size);

            Assert.True(bounds.X >= -1, $"{control.GetType().Name} is clipped on the left.");
            Assert.True(bounds.Y >= -1, $"{control.GetType().Name} is clipped at the top.");
            Assert.True(
                bounds.Right <= window.ClientSize.Width + 1,
                $"{control.GetType().Name} is clipped on the right."
            );
            Assert.True(
                bounds.Bottom <= window.ClientSize.Height + 1,
                $"{control.GetType().Name} is clipped at the bottom."
            );
        }

        コントロールの重なりがないことを確認する(
            window,
            [
                window.テスト用画面.危険絞り込み,
                window.テスト用画面.警告絞り込み,
                window.テスト用画面.注意絞り込み,
                window.テスト用画面.検索絞り込み,
                window.テスト用画面.解析ボタン,
                window.テスト用画面.キャンセルボタン,
                window.テスト用画面.表示言語選択,
                window.テスト用画面.テーマ選択,
            ]
        );
    }

    private static void コントロールの重なりがないことを確認する(
        メインウィンドウ window,
        IReadOnlyList<Control> controls
    )
    {
        for (var leftIndex = 0; leftIndex < controls.Count; leftIndex++)
        {
            var left = 相対境界を取得する(window, controls[leftIndex]);
            for (var rightIndex = leftIndex + 1; rightIndex < controls.Count; rightIndex++)
            {
                var right = 相対境界を取得する(window, controls[rightIndex]);
                var intersection = left.Intersect(right);
                Assert.True(
                    intersection.Width <= 1 || intersection.Height <= 1,
                    $"{controls[leftIndex].GetType().Name} overlaps {controls[rightIndex].GetType().Name}."
                );
            }
        }
    }

    private static Rect 相対境界を取得する(Window window, Control control)
    {
        var origin = control.TranslatePoint(new Point(0, 0), window);
        Assert.True(origin.HasValue);
        return new Rect(origin!.Value, control.Bounds.Size);
    }

    private static void 画面を撮影する(Window window, string scenario, string language)
    {
        画面更新を完了する();

        if (!画像回帰確認が要求されている())
        {
            return;
        }

        標準画像環境を確認する();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var baselinePath = Path.Combine(画像基準ルートを取得する(), $"{scenario}.png");
        var evidenceDirectory = Path.Combine(証跡ルートを取得する(), scenario);
        Directory.CreateDirectory(evidenceDirectory);

        var actualPath = Path.Combine(evidenceDirectory, "actual.png");
        var diffPath = Path.Combine(evidenceDirectory, "diff.png");
        frame.Save(actualPath, PngBitmapEncoderOptions.Default);

        UI状態を書き出す(window, scenario, language, evidenceDirectory);

        if (Environment.GetEnvironmentVariable("UPDATE_VISUAL_BASELINES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.Copy(actualPath, baselinePath, overwrite: true);
            return;
        }

        Assert.True(File.Exists(baselinePath), $"Missing visual baseline: {baselinePath}");
        var changedRatio = 差分画像を作る(baselinePath, actualPath, diffPath);
        Assert.True(
            changedRatio <= MaximumChangedPixelRatio,
            $"Visual regression for {scenario}: {changedRatio:P3} pixels changed; "
                + $"threshold is {MaximumChangedPixelRatio:P3}. Diff: {diffPath}"
        );
    }

    private static void 画像基準用fixture位置を固定する(
        メインウィンドウ window,
        string fixtureRoot,
        string sourcePath
    )
    {
        if (!画像回帰確認が要求されている())
        {
            return;
        }

        // fixtureの保存先ではなく画面レイアウトを画像で検証し、移行による誤検出を防ぐ。
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(リポジトリルートを取得する(), "..", "..")
        );
        var relativePath = Path.GetRelativePath(fixtureRoot, sourcePath);
        var stablePath = Path.Combine(
            repositoryRoot,
            "tests",
            "fixtures",
            "release-validation-csharp",
            relativePath
        );
        window.テスト用画面.詳細位置.Text = (
            window.テスト用画面.詳細位置.Text ?? string.Empty
        ).Replace(sourcePath, stablePath, StringComparison.Ordinal);
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

    private static double 差分画像を作る(string expectedPath, string actualPath, string diffPath)
    {
        using var expected = SKBitmap.Decode(expectedPath);
        using var actual = SKBitmap.Decode(actualPath);

        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        using var diff = new SKBitmap(
            actual.Width,
            actual.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );

        long changedPixels = 0;
        var totalPixels = (long)actual.Width * actual.Height;
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                var expectedPixel = expected.GetPixel(x, y);
                var actualPixel = actual.GetPixel(x, y);
                var delta = Math.Max(
                    Math.Max(
                        Math.Abs(expectedPixel.Red - actualPixel.Red),
                        Math.Abs(expectedPixel.Green - actualPixel.Green)
                    ),
                    Math.Max(
                        Math.Abs(expectedPixel.Blue - actualPixel.Blue),
                        Math.Abs(expectedPixel.Alpha - actualPixel.Alpha)
                    )
                );

                if (delta > PixelDeltaThreshold)
                {
                    changedPixels++;
                    diff.SetPixel(x, y, SKColors.Red);
                }
                else
                {
                    diff.SetPixel(
                        x,
                        y,
                        new SKColor(actualPixel.Red, actualPixel.Green, actualPixel.Blue, 64)
                    );
                }
            }
        }

        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(diffPath);
        data.SaveTo(stream);

        return totalPixels == 0 ? 0 : (double)changedPixels / totalPixels;
    }

    private static void UI状態を書き出す(
        Window window,
        string scenario,
        string language,
        string evidenceDirectory
    )
    {
        var state = new
        {
            scenario,
            language,
            width = window.ClientSize.Width,
            height = window.ClientSize.Height,
            title = window.Title,
            theme = window.ActualThemeVariant.ToString(),
            visibleRowCount = window is メインウィンドウ main
                ? 表示行を取得する(main).Length
                : (int?)null,
            status = window is メインウィンドウ statusWindow
                ? statusWindow.テスト用画面.状態表示.Text
                : null,
            selectedRule = window is メインウィンドウ selectedWindow
            && selectedWindow.テスト用画面.診断一覧.SelectedItem is DiagnosticRow row
                ? row.RuleId
                : null,
            selectedFile = window is メインウィンドウ selectedFileWindow
            && selectedFileWindow.テスト用画面.診断一覧.SelectedItem is DiagnosticRow fileRow
                ? fileRow.FileName
                : null,
            summary = window is メインウィンドウ summaryWindow
                ? new
                {
                    danger = summaryWindow.テスト用画面.危険件数表示.Text,
                    warning = summaryWindow.テスト用画面.警告件数表示.Text,
                    attention = summaryWindow.テスト用画面.注意件数表示.Text,
                    configuration = summaryWindow.テスト用画面.設定概要.Text,
                }
                : null,
        };

        File.WriteAllText(
            Path.Combine(evidenceDirectory, "ui-state.json"),
            JsonSerializer.Serialize(state, IndentedJson)
        );
    }

    private static int 期待する診断件数を読む(string fixtureRoot)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixtureRoot, "expected-diagnostics.json"))
        );
        return document.RootElement.GetProperty("exactDiagnostics").GetArrayLength();
    }

    private static string fixtureルートを取得する() =>
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
        var configured = Environment.GetEnvironmentVariable("UI_EVIDENCE_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "oop-design-checker-ui-evidence")
            : configured;
    }

    private static string リポジトリルートを取得する()
    {
        foreach (
            var candidate in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        )
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "OopDesignChecker.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
