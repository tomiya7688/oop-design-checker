using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "JSONとC#から生成した解析用設定の不変条件を検証します。",
  "フィールド": []
}
*/
public sealed class 設定不変条件契約テスト
{
    /*
    {
      "責務": "非nullの設定と有効な継承深度を、生成時に保証します。",
      "処理": ["同じ不正値をC#とJSONから入力します。", "生成時に同じエラー説明で拒否することを確認します。"],
      "引数": {"入力例": "生成する不正値の種類です。", "JSON": "同じ不正値のJSON表現です。", "説明": "既存のエラー説明です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(0, "{\"ignoredPaths\":null}", "ignoredPaths must be an array.")]
    [InlineData(1, "{\"disabledRules\":null}", "disabledRules must be an array.")]
    [InlineData(2, "{\"ruleSettings\":null}", "ruleSettings must be an object.")]
    [InlineData(
        3,
        "{\"ruleSettings\":{\"oop304\":null}}",
        "ruleSettings.oop304 must be an object."
    )]
    [InlineData(
        4,
        "{\"ruleSettings\":{\"oop304\":{\"warningDepth\":0}}}",
        "ruleSettings.oop304.warningDepth must be at least 1."
    )]
    [InlineData(
        5,
        "{\"ruleSettings\":{\"oop304\":{\"warningDepth\":-1}}}",
        "ruleSettings.oop304.warningDepth must be at least 1."
    )]
    public void 不正な設定は生成時に拒否する(int 入力例, string JSON, string 説明)
    {
        var CSharp例外 = Assert.Throws<InvalidOperationException>(() =>
            不正な設定を生成する(入力例)
        );
        var JSON例外 = Assert.Throws<InvalidOperationException>(() =>
            CheckerConfigurationJson.Parse(JSON)
        );
        Assert.Equal(説明, CSharp例外.Message);
        Assert.Equal(説明, JSON例外.Message);
    }

    /*
    {
      "責務": "未定義severityを解析用設定へ保存できないことを確認します。",
      "処理": ["未定義の列挙値を直接指定します。", "initの時点で拒否することを確認します。"],
      "引数": {"値": "未定義の整数値です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public void 未定義の失敗基準を生成時に拒否する(int 値)
    {
        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            new CheckerConfiguration { FailureThreshold = (DesignDiagnosticSeverity)値 }
        );
        Assert.Equal("failureThreshold must be a defined severity.", 例外.Message);
    }

    /*
    {
      "責務": "有効な重大度と深度の上下境界で公開APIとJSON形式を維持します。",
      "処理": ["initで設定を構築し、互換ValidateとJSON往復を実行します。", "値と規則生成を確認します。"],
      "引数": {"重大度": "有効な失敗基準です。", "警告深度": "受理する深度です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(DesignDiagnosticSeverity.Attention, 1)]
    [InlineData(DesignDiagnosticSeverity.Warning, 4)]
    [InlineData(DesignDiagnosticSeverity.Danger, int.MaxValue)]
    public void 有効な設定をJSONで往復する(DesignDiagnosticSeverity 重大度, int 警告深度)
    {
        var 設定 = new CheckerConfiguration
        {
            IgnoredPaths = ["**/Generated/**"],
            DisabledRules = ["OOP106"],
            FailureThreshold = 重大度,
            RuleSettings = new() { Oop304 = new() { WarningDepth = 警告深度 } },
        };
        CheckerConfigurationJson.Validate(設定);
        var JSON = CheckerConfigurationJson.Serialize(設定);
        var 往復結果 = CheckerConfigurationJson.Parse(JSON);

        Assert.Equal(重大度, 往復結果.FailureThreshold);
        Assert.Equal(警告深度, 往復結果.RuleSettings.Oop304.WarningDepth);
        Assert.Equal("**/Generated/**", Assert.Single(往復結果.IgnoredPaths));
        Assert.Equal("OOP106", Assert.Single(往復結果.DisabledRules));
        Assert.Equal(JSON, CheckerConfigurationJson.Serialize(往復結果));
        Assert.Equal(24, 規則一覧.既定の規則一覧を作る(往復結果).Count);
    }

    /*
    {
      "責務": "省略した設定の既定値と既存のnull入力エラーを維持します。",
      "処理": ["既定設定と空JSONを比較します。", "公開変換APIのnull入力を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 既定値と公開検証APIを維持する()
    {
        var 設定 = new CheckerConfiguration();
        Assert.Empty(設定.IgnoredPaths);
        Assert.Empty(設定.DisabledRules);
        Assert.Equal(DesignDiagnosticSeverity.Danger, 設定.FailureThreshold);
        Assert.Equal(4, 設定.RuleSettings.Oop304.WarningDepth);
        Assert.Equal(
            CheckerConfigurationJson.Serialize(設定),
            CheckerConfigurationJson.Serialize(CheckerConfigurationJson.Parse("{}"))
        );
        Assert.Throws<ArgumentNullException>(() => CheckerConfigurationJson.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => CheckerConfigurationJson.Serialize(null!));
        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            CheckerConfigurationJson.Parse("null")
        );
        Assert.Equal("The checker configuration is empty.", 例外.Message);
    }

    /*
    {
      "責務": "JSON変換を通さない不正な構築例を実行します。",
      "処理": ["指定されたプロパティへ不正値を渡します。"],
      "引数": {"入力例": "不正値の種類です。"},
      "戻り値": "検証がなければ構築できてしまう設定です。"
    }
    */
    private static object 不正な設定を生成する(int 入力例) =>
        入力例 switch
        {
            0 => new CheckerConfiguration { IgnoredPaths = null! },
            1 => new CheckerConfiguration { DisabledRules = null! },
            2 => new CheckerConfiguration { RuleSettings = null! },
            3 => new CheckerRuleSettings { Oop304 = null! },
            4 => new InheritanceDepthRuleSettings { WarningDepth = 0 },
            5 => new InheritanceDepthRuleSettings { WarningDepth = -1 },
            _ => throw new ArgumentOutOfRangeException(nameof(入力例)),
        };
}
