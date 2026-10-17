using OopDesignChecker.Gui;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

/*
{
  "責務": {
    "UI自動化遅延契約テスト": "自動テスト専用の遅延設定が、既定値と30秒上限の契約に従うことを確認します。"
  },
  "フィールド": []
}
*/
public sealed class UI自動化遅延契約テスト
{
    /*
    {
      "責務": {
        "遅延設定の採用値を検証する": "環境変数を変更せず、設定文字列の成功、境界、無効値を検証します。"
      },
      "処理": [
        "環境変数を変更せず、設定文字列の成功、境界、無効値を検証します。"
      ],
      "引数": {
        "設定値": "環境変数に指定する文字列です。",
        "期待ミリ秒": "採用される遅延のミリ秒数です。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData(" ", 0)]
    [InlineData("0", 0)]
    [InlineData("2500", 2500)]
    [InlineData("30000", 30000)]
    [InlineData(" 2500 ", 2500)]
    [InlineData("30001", 0)]
    [InlineData("-1", 0)]
    [InlineData("invalid", 0)]
    [InlineData("1.5", 0)]
    [InlineData("9999999999999", 0)]
    public void 遅延設定の採用値を検証する(string? 設定値, int 期待ミリ秒) =>
        Assert.Equal(期待ミリ秒, UI自動化設定.解析遅延を読み取る(設定値));
}
