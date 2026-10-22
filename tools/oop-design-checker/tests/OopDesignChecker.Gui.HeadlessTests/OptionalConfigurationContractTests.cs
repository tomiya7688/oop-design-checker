using Avalonia.Controls;
using Avalonia.Interactivity;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

public sealed partial class 画面視覚回帰テスト
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 設定欄が空でも既定値で解析を完了する()
    {
        var 一時ルート = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-gui-no-config",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(一時ルート);
        var 対象パス = Path.Combine(一時ルート, "Sample.cs");
        File.WriteAllText(
            対象パス,
            "internal sealed class Sample { private int _value; public int Value => _value; }"
        );

        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            var 画面 = ウィンドウ.テスト用画面;
            画面.対象パス欄.Text = 対象パス;
            画面.設定パス欄.Text = string.Empty;

            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            条件成立まで待つ(
                () =>
                    (画面.状態表示.Text ?? string.Empty).StartsWith(
                        "完了:",
                        StringComparison.Ordinal
                    ),
                タイムアウト時間: 10000
            );

            Assert.Equal(string.Empty, 画面.設定パス欄.Text);
            Assert.Contains("既定値", 画面.設定概要.Text ?? string.Empty, StringComparison.Ordinal);
            Assert.True(画面.解析ボタン.IsEnabled);
            Assert.False(画面.キャンセルボタン.IsEnabled);
        }
        finally
        {
            ウィンドウ.Close();
            if (Directory.Exists(一時ルート))
            {
                Directory.Delete(一時ルート, recursive: true);
            }
        }
    }
}
