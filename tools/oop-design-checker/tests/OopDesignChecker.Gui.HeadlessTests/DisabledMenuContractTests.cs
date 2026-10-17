using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

/*
{
  "責務": "無効なメニュー操作がキー入力でも実行されないことを確認します。",
  "フィールド": []
}
*/
public sealed partial class 画面視覚回帰テスト
{
    /*
    {
      "責務": "解析中の操作禁止と、解析終了後の操作復帰を検証します。",
      "処理": [
        "テスト用遅延で解析を開始します。",
        "無効メニューへのキー入力を検査します。",
        "キャンセル後に同じキーから操作できることを確認します。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 解析中の無効メニューをアクセスキーで迂回しない()
    {
        const string 遅延変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";
        var 変更前の遅延 = Environment.GetEnvironmentVariable(遅延変数名);
        Environment.SetEnvironmentVariable(遅延変数名, "30000");
        var ウィンドウ = ウィンドウを作る(1280, 820);
        var 画面 = ウィンドウ.テスト用画面;
        var メニュー = 画面.メインメニュー部品;
        try
        {
            画面.設定パス欄.Text = "keep-configuration.json";
            var 表示言語 = 画面.表示言語選択.SelectedIndex;
            var テーマ = 画面.テーマ選択.SelectedIndex;
            画面.解析ボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            画面更新を完了する();
            Assert.True(画面.キャンセルボタン.IsEnabled);

            // 各キー経路が、マウス操作に使う同じMenuItemの利用可否を尊重することを検査します。
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.対象ファイル項目,
                GUIコマンド.対象ファイル選択
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.対象フォルダー項目,
                GUIコマンド.対象フォルダー選択
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.設定項目,
                GUIコマンド.設定選択
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.設定編集項目,
                GUIコマンド.設定編集
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.設定消去項目,
                GUIコマンド.設定消去
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.JSON出力項目,
                GUIコマンド.JSON出力
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.SARIF出力項目,
                GUIコマンド.SARIF出力
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.解析メニュー,
                メニュー.解析項目,
                GUIコマンド.解析開始
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.解析メニュー,
                メニュー.ソースを開く項目,
                GUIコマンド.ソースを開く
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.表示メニュー,
                メニュー.言語メニュー,
                GUIコマンド.言語メニュー
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.表示メニュー,
                メニュー.テーマメニュー,
                GUIコマンド.テーマメニュー
            );
            Assert.Equal("keep-configuration.json", 画面.設定パス欄.Text);
            Assert.Equal(表示言語, 画面.表示言語選択.SelectedIndex);
            Assert.Equal(テーマ, 画面.テーマ選択.SelectedIndex);

            画面.キャンセルボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            条件成立まで待つ(() => 画面.解析ボタン.IsEnabled, タイムアウト時間: 3000);
            メニュー.ファイルメニュー.IsSubMenuOpen = true;
            キーを押す(
                ウィンドウ,
                GUIコマンド.設定消去.メニューアクセスキー!.Value,
                PhysicalKey.None
            );
            Assert.Equal(string.Empty, 画面.設定パス欄.Text);
            Assert.False(メニュー.ファイルメニュー.IsSubMenuOpen);
        }
        finally
        {
            画面.キャンセルボタン.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            画面更新を完了する();
            ウィンドウ.Close();
            Environment.SetEnvironmentVariable(遅延変数名, 変更前の遅延);
        }
    }

    /*
    {
      "責務": "結果や選択がない場合の操作禁止を検証します。",
      "処理": [
        "空の画面へ出力とソース表示のアクセスキーを送ります。",
        "無効な操作でメニューを閉じたり別画面を開いたりしないことを確認します。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void 結果と選択がない操作をアクセスキーで迂回しない()
    {
        var ウィンドウ = ウィンドウを作る(1280, 820);
        try
        {
            var メニュー = ウィンドウ.テスト用画面.メインメニュー部品;
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.JSON出力項目,
                GUIコマンド.JSON出力
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.ファイルメニュー,
                メニュー.SARIF出力項目,
                GUIコマンド.SARIF出力
            );
            無効なメニュー操作を試す(
                ウィンドウ,
                メニュー.解析メニュー,
                メニュー.ソースを開く項目,
                GUIコマンド.ソースを開く
            );
            Assert.Empty(ウィンドウ.OwnedWindows);
        }
        finally
        {
            ウィンドウ.Close();
        }
    }

    /*
    {
      "責務": "無効項目の実行に先立つメニューの閉鎖やサブメニュー表示が起きないことを検証します。",
      "処理": [
        "親メニューを開きます。",
        "対象操作のアクセスキーを入力し、画面状態を確認します。"
      ],
      "引数": {
        "ウィンドウ": "キー入力先です。",
        "親メニュー": "対象操作を含む最上位メニューです。",
        "項目": "無効になっているMenuItemです。",
        "コマンド": "対象操作のキー定義です。"
      },
      "戻り値": []
    }
    */
    private static void 無効なメニュー操作を試す(
        メインウィンドウ ウィンドウ,
        MenuItem 親メニュー,
        MenuItem 項目,
        GUIコマンド定義 コマンド
    )
    {
        親メニュー.IsSubMenuOpen = true;
        画面更新を完了する();
        Assert.False(項目.IsEffectivelyEnabled);
        キーを押す(ウィンドウ, コマンド.メニューアクセスキー!.Value, PhysicalKey.None);
        Assert.True(親メニュー.IsSubMenuOpen);
        Assert.False(項目.IsSubMenuOpen);
        親メニュー.IsSubMenuOpen = false;
    }
}
