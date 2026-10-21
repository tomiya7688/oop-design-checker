using OopDesignChecker.Configuration;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "設定リストの所有権とJSON入力の互換性を検証します。",
  "フィールド": []
}
*/
public sealed class 設定所有権契約テスト
{
    /*
    {
      "責務": "構築後の入力変更と公開一覧への変更から設定を保護します。",
      "処理": ["配列またはListを設定へ渡してから書き換えます。", "値とJSONが維持され、キャストによる更新も拒否されることを確認します。"],
      "引数": {"配列を使う": "配列とListの両方を確認します。", "無視パスを使う": "対象の設定リストを選びます。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void 設定が独立したリストを所有する(bool 配列を使う, bool 無視パスを使う)
    {
        IList<string> 入力 = 配列を使う ? new[] { "original" } : new List<string> { "original" };
        var 設定 = 無視パスを使う
            ? new CheckerConfiguration { IgnoredPaths = (IReadOnlyList<string>)入力 }
            : new CheckerConfiguration { DisabledRules = (IReadOnlyList<string>)入力 };
        var 元のJSON = CheckerConfigurationJson.Serialize(設定);
        入力[0] = "changed";
        if (!配列を使う)
            入力.Clear();

        var 一覧 = 無視パスを使う ? 設定.IgnoredPaths : 設定.DisabledRules;
        Assert.NotSame(入力, 一覧);
        Assert.Equal("original", Assert.Single(一覧));
        Assert.Equal(元のJSON, CheckerConfigurationJson.Serialize(設定));
        var 公開一覧 = Assert.IsAssignableFrom<IList<string>>(一覧);
        Assert.True(公開一覧.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => 公開一覧[0] = "changed");
        Assert.Throws<NotSupportedException>(() => 公開一覧.Add("changed"));
    }

    /*
    {
      "責務": "JSONでは重複キーの最終値を検証する既存契約を保ちます。",
      "処理": ["途中の不正値を後の正常値で上書きしたJSONを読み込みます。", "最終的な設定が正常であることを確認します。"],
      "引数": {"JSON": "重複キーを含む入力です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("{\"ignoredPaths\":null,\"ignoredPaths\":[]}")]
    [InlineData("{\"disabledRules\":null,\"disabledRules\":[]}")]
    [InlineData("{\"ruleSettings\":null,\"ruleSettings\":{}}")]
    [InlineData("{\"ruleSettings\":{\"oop304\":null,\"oop304\":{}}}")]
    [InlineData("{\"ruleSettings\":{\"oop304\":{\"warningDepth\":0,\"warningDepth\":4}}}")]
    public void JSONは最後の値を検証する(string JSON)
    {
        var 設定 = CheckerConfigurationJson.Parse(JSON);
        Assert.Equal(4, 設定.RuleSettings.Oop304.WarningDepth);
        CheckerConfigurationJson.Validate(設定);
    }

    /*
    {
      "責務": "JSONのキー順によらず、既存と同じ順序で不正な項目を説明します。",
      "処理": ["複数の不正な最終値を渡し、無視対象リストが先に説明されることを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 最終値のエラー説明順を維持する()
    {
        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            CheckerConfigurationJson.Parse(
                """{"ruleSettings":null,"disabledRules":null,"ignoredPaths":null}"""
            )
        );
        Assert.Equal("ignoredPaths must be an array.", 例外.Message);
    }

    /*
    {
      "責務": "形式変換エラーの説明と整数severityの拒否を維持します。",
      "処理": ["JSON型や列挙値が不正な入力を読み込み、変換エラーとして説明することを確認します。"],
      "引数": {"JSON": "形式が不正な入力です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("{\"failureThreshold\":999}")]
    [InlineData("{\"failureThreshold\":\"unknown\"}")]
    [InlineData("{\"ignoredPaths\":42}")]
    [InlineData("{\"ruleSettings\":[]}")]
    [InlineData("{\"ruleSettings\":{\"oop304\":{\"warningDepth\":\"deep\"}}}")]
    [InlineData("{")]
    public void JSON形式エラーを説明する(string JSON)
    {
        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            CheckerConfigurationJson.Parse(JSON)
        );
        Assert.StartsWith("Invalid checker configuration JSON:", 例外.Message);
        Assert.IsType<System.Text.Json.JsonException>(例外.InnerException);
    }

    /*
    {
      "責務": "コメント、大小文字、末尾カンマ、未知のキーを許容する契約を維持します。",
      "処理": ["緩やかなJSON入力を読み込み、既定値が使用されることを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void JSON入力の許容形式を維持する()
    {
        var 設定 = CheckerConfigurationJson.Parse(
            """{/*comment*/"RULESETTINGS":{"OOP304":{},},"unknown":true,}"""
        );
        Assert.Equal(4, 設定.RuleSettings.Oop304.WarningDepth);
        Assert.Empty(設定.DisabledRules);
    }
}
