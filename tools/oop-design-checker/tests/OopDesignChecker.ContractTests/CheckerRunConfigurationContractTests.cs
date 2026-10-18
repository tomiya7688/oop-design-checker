using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "解析結果が保持する設定の所有権と実効失敗基準を検証します。",
  "フィールド": []
}
*/
public sealed class 解析結果設定契約テスト
{
    /*
    {
      "責務": "設定を初期化または差し替えた時点で、リストの所有権を分離します。",
      "処理": ["設定を結果へ渡してから入力リストを書き換えます。", "結果の設定、失敗基準、ハッシュ値とJSONが維持されることを確認します。"],
      "引数": {"配列を使う": "設定リストの実装を選びます。", "withを使う": "設定の差し替え方法を選びます。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void 設定リストを独立して保持する(bool 配列を使う, bool withを使う)
    {
        IList<string> 無視対象 = 配列を使う
            ? new[] { "**/Generated/**" }
            : new List<string> { "**/Generated/**" };
        IList<string> 無効規則 = 配列を使う ? new[] { "OOP106" } : new List<string> { "OOP106" };
        var 設定 = new CheckerConfiguration
        {
            IgnoredPaths = (IReadOnlyList<string>)無視対象,
            DisabledRules = (IReadOnlyList<string>)無効規則,
            FailureThreshold = DesignDiagnosticSeverity.Warning,
            RuleSettings = new() { Oop304 = new() { WarningDepth = 9 } },
        };
        var 元の結果 = new CheckerRunResult([], DesignDiagnosticSeverity.Danger, "settings.json");
        var 結果 = withを使う
            ? 元の結果 with
            {
                Configuration = 設定,
            }
            : new CheckerRunResult([], DesignDiagnosticSeverity.Danger, "settings.json")
            {
                Configuration = 設定,
            };
        var 元のJSON = CheckerConfigurationJson.Serialize(結果.Configuration);
        var 元のハッシュ値 = 結果.GetHashCode();

        // 設定の元リストを変えても、実行済み結果の説明を変えてはいけません。
        無視対象[0] = "**/*.cs";
        無効規則[0] = "OOP304";
        if (!配列を使う)
        {
            無視対象.Clear();
            無効規則.Add("OOP201");
        }

        Assert.NotSame(設定, 結果.Configuration);
        Assert.Equal("**/Generated/**", Assert.Single(結果.Configuration.IgnoredPaths));
        Assert.Equal("OOP106", Assert.Single(結果.Configuration.DisabledRules));
        Assert.Equal(9, 結果.Configuration.RuleSettings.Oop304.WarningDepth);
        Assert.Equal(DesignDiagnosticSeverity.Warning, 結果.Configuration.FailureThreshold);
        Assert.Equal(DesignDiagnosticSeverity.Danger, 結果.FailureThreshold);
        Assert.Equal("settings.json", 結果.ConfigurationPath);
        Assert.False(結果.ShouldFail);
        Assert.Equal(元のJSON, CheckerConfigurationJson.Serialize(結果.Configuration));
        Assert.Equal(元のハッシュ値, 結果.GetHashCode());
        Assert.Empty(元の結果.Configuration.DisabledRules);

        // 結果から取得した設定についても、キャストによる変更を拒否します。
        var 公開無視対象 = Assert.IsAssignableFrom<IList<string>>(結果.Configuration.IgnoredPaths);
        var 公開無効規則 = Assert.IsAssignableFrom<IList<string>>(結果.Configuration.DisabledRules);
        Assert.Throws<NotSupportedException>(() => 公開無視対象[0] = "changed");
        Assert.Throws<NotSupportedException>(() => 公開無効規則.Clear());
    }

    /*
    {
      "責務": "既定設定の値と変更できない空リストを維持します。",
      "処理": ["設定を指定しない結果とwith複製について既定値を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 既定設定を維持する()
    {
        var 結果 = new CheckerRunResult([], DesignDiagnosticSeverity.Attention, null);
        var 複製 = 結果 with { };
        Assert.Equal(結果, 複製);
        Assert.Equal(DesignDiagnosticSeverity.Danger, 結果.Configuration.FailureThreshold);
        Assert.Equal(4, 結果.Configuration.RuleSettings.Oop304.WarningDepth);
        Assert.Empty(結果.Configuration.IgnoredPaths);
        Assert.Empty(結果.Configuration.DisabledRules);
        var 一覧 = Assert.IsAssignableFrom<IList<string>>(結果.Configuration.IgnoredPaths);
        Assert.Throws<NotSupportedException>(() => 一覧.Add("changed"));
    }

    /*
    {
      "責務": "公開サービスが設定と上書き後の実効基準を別々に保存することを確認します。",
      "処理": ["設定ファイルを用いた解析で失敗基準を上書きします。", "診断、設定値、実効基準と設定パスを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void サービスは設定値と実効基準を保持する()
    {
        using var 環境 = new 一時解析環境();
        var 設定パス = 環境.ファイルを書く(
            "oop-design-checker.json",
            """{"failureThreshold":"danger","ruleSettings":{"oop304":{"warningDepth":7}}}"""
        );
        環境.ファイルを書く("Sample.cs", "internal sealed class Sample { public int Value; }");
        var 結果 = CheckerService.Analyze(
            環境.ルート,
            failureThresholdOverride: DesignDiagnosticSeverity.Attention,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Contains(結果.Diagnostics, 診断 => 診断.Rule.Id == "OOP106");
        Assert.True(結果.ShouldFail);
        Assert.Equal(DesignDiagnosticSeverity.Attention, 結果.FailureThreshold);
        Assert.Equal(DesignDiagnosticSeverity.Danger, 結果.Configuration.FailureThreshold);
        Assert.Equal(7, 結果.Configuration.RuleSettings.Oop304.WarningDepth);
        Assert.Equal(設定パス, 結果.ConfigurationPath);
    }

    /*
    {
      "責務": "コピーできないnull入力は結果を公開する前に拒否します。",
      "処理": ["コンストラクターとinit経由のnull一覧、null設定を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void null一覧とnull設定を拒否する()
    {
        var 結果 = new CheckerRunResult([], DesignDiagnosticSeverity.Danger, null);
        Assert.Throws<ArgumentNullException>(() =>
            new CheckerRunResult(null!, DesignDiagnosticSeverity.Danger, null)
        );
        Assert.Throws<ArgumentNullException>(() => 結果 with { Diagnostics = null! });
        Assert.Throws<ArgumentNullException>(() => 結果 with { Configuration = null! });
    }
}
