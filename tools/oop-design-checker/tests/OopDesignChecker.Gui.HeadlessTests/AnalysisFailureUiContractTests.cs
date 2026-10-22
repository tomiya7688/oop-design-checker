using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using OopDesignChecker.Localization;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

public sealed partial class 画面視覚回帰テスト
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 対象エラーを対象欄へ表示し修正時に消す()
    {
        using var 環境 = new GUI失敗テスト環境();
        var 正常ソース = 環境.書く(
            "Valid.cs",
            "internal sealed class Valid { private int _value; public int Value => _value; }"
        );
        var 存在しない対象 = Path.Combine(環境.ルート, "missing.cs");
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            var 画面 = ウィンドウ.テスト用画面;
            画面.対象パス欄.Text = 存在しない対象;
            画面.設定パス欄.Text = string.Empty;
            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            条件成立まで待つ(
                () => 画面.対象エラー.IsVisible && 画面.解析ボタン.IsEnabled,
                タイムアウト時間: 10000
            );

            Assert.StartsWith("⚠ ", 画面.対象エラー.Text, StringComparison.Ordinal);
            Assert.Contains("見つかりません", 画面.対象エラー.Text, StringComparison.Ordinal);
            Assert.False(画面.設定エラー.IsVisible);
            Assert.Contains("「対象」", 画面.状態表示.Text, StringComparison.Ordinal);
            Assert.Same(画面.対象パス欄, ウィンドウ.FocusManager?.GetFocusedElement());

            画面.対象パス欄.Text = 正常ソース;
            画面更新を完了する();
            Assert.False(画面.対象エラー.IsVisible);
            Assert.Equal("準備完了", 画面.状態表示.Text);
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 設定エラーを設定欄へ表示して日英とテーマを保つ()
    {
        using var 環境 = new GUI失敗テスト環境();
        var 正常ソース = 環境.書く(
            "Valid.cs",
            "internal sealed class Valid { private int _value; public int Value => _value; }"
        );
        var 存在しない設定 = Path.Combine(環境.ルート, "missing-config.json");
        var 壊れた設定 = 環境.書く("broken-config.json", "{ not-json");
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            var 画面 = ウィンドウ.テスト用画面;
            画面.対象パス欄.Text = 正常ソース;
            画面.設定パス欄.Text = 存在しない設定;
            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            条件成立まで待つ(
                () => 画面.設定エラー.IsVisible && 画面.解析ボタン.IsEnabled,
                タイムアウト時間: 10000
            );

            Assert.False(画面.対象エラー.IsVisible);
            Assert.Contains("設定ファイル", 画面.設定エラー.Text, StringComparison.Ordinal);
            Assert.Contains("「設定」", 画面.状態表示.Text, StringComparison.Ordinal);
            Assert.Same(画面.設定パス欄, ウィンドウ.FocusManager?.GetFocusedElement());

            ウィンドウ.テスト用表示言語を設定する(UserInterfaceLanguage.English);
            画面更新を完了する();
            Assert.Contains(
                "Configuration",
                画面.設定エラー.Text,
                StringComparison.OrdinalIgnoreCase
            );
            Assert.Contains("Configuration field", 画面.状態表示.Text, StringComparison.Ordinal);

            ウィンドウ.テスト用テーマを設定する(ThemeVariant.Light);
            画面更新を完了する();
            Assert.True(画面.設定エラー.IsVisible);

            画面.設定パス欄.Text = 壊れた設定;
            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            条件成立まで待つ(
                () => 画面.設定エラー.IsVisible && 画面.解析ボタン.IsEnabled,
                タイムアウト時間: 10000
            );
            Assert.Contains(
                "configuration",
                画面.設定エラー.Text,
                StringComparison.OrdinalIgnoreCase
            );
        }
        finally
        {
            ウィンドウ.テスト用テーマを設定する(ThemeVariant.Dark);
            ウィンドウ.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void project読込失敗とcompile失敗を対象欄で区別する()
    {
        using var 環境 = new GUI失敗テスト環境();
        var 壊れたSLNX = 環境.書く("Broken.slnx", "<Solution><Project");
        var 壊れたソース = 環境.書く(
            "Broken.cs",
            "internal sealed class Broken { UnknownType Value; }"
        );
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            var 画面 = ウィンドウ.テスト用画面;
            画面.設定パス欄.Text = string.Empty;
            画面.対象パス欄.Text = 壊れたSLNX;
            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            条件成立まで待つ(
                () => 画面.対象エラー.IsVisible && 画面.解析ボタン.IsEnabled,
                タイムアウト時間: 10000
            );
            Assert.Contains("プロジェクトを読み込めません", 画面.状態表示.Text, StringComparison.Ordinal);

            画面.対象パス欄.Text = 壊れたソース;
            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            条件成立まで待つ(
                () =>
                    画面.対象エラー.IsVisible
                    && (画面.状態表示.Text ?? string.Empty).Contains(
                        "コンパイルエラー",
                        StringComparison.Ordinal
                    )
                    && 画面.解析ボタン.IsEnabled,
                タイムアウト時間: 10000
            );
            Assert.Contains("コンパイルエラー", 画面.対象エラー.Text, StringComparison.Ordinal);
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    private sealed class GUI失敗テスト環境 : IDisposable
    {
        public string ルート { get; } = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-gui-failure",
            Guid.NewGuid().ToString("N")
        );

        public GUI失敗テスト環境()
        {
            Directory.CreateDirectory(ルート);
        }

        public string 書く(string 名前, string 内容)
        {
            var パス = Path.Combine(ルート, 名前);
            File.WriteAllText(パス, 内容);
            return パス;
        }

        public void Dispose()
        {
            if (Directory.Exists(ルート))
            {
                Directory.Delete(ルート, recursive: true);
            }
        }
    }
}
