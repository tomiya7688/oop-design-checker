using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "解析ユースケースをファイルシステムとMSBuildから独立して検証します。",
  "フィールド": {"解析段階の順序": "従来の依存呼び出し順です。"}
}
*/
public sealed class 解析ユースケース契約テスト
{
    private static readonly string[] 解析段階の順序 =
    [
        "入力",
        "規則",
        "ローダー生成",
        "プロジェクト",
        "診断",
    ];

    /*
    {
      "責務": "対象、設定、規則、プロジェクトと通知をそれぞれの依存へ正しく渡します。",
      "処理": ["メモリ上の依存だけで解析を実行します。", "順序、引数、無効規則の除外、結果の値を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 依存を差し替えて解析結果を組み立てる()
    {
        var 環境 = new 解析ユースケース検証環境();
        var 通知 = TestContext.Current.CancellationToken;
        var 結果 = 環境.作る().実行する("virtual-input", "requested-config", キャンセル通知: 通知);

        Assert.Equal(解析段階の順序, 環境.呼出順序);
        Assert.Equal("virtual-input", 環境.指定対象);
        Assert.Equal("requested-config", 環境.指定設定パス);
        Assert.Equal("virtual-resolved-target", 環境.読込対象);
        Assert.Same(環境.設定, 環境.規則供給時の設定);
        Assert.Same(環境.設定.IgnoredPaths, 環境.ローダー用の無視パス群);
        Assert.Equal("KEEP", Assert.Single(環境.実行規則群!).規則情報.Id);
        Assert.Same(環境.プロジェクト群, 環境.実行プロジェクト群);
        Assert.Equal(通知, 環境.読込時の通知);
        Assert.Equal(通知, 環境.解析時の通知);
        Assert.Same(Assert.Single(環境.診断群), Assert.Single(結果.Diagnostics));
        Assert.Equal("virtual-resolved-config", 結果.ConfigurationPath);
        Assert.Equal(環境.設定.IgnoredPaths, 結果.Configuration.IgnoredPaths);
        Assert.Equal(環境.設定.DisabledRules, 結果.Configuration.DisabledRules);
        Assert.Equal(DesignDiagnosticSeverity.Warning, 結果.FailureThreshold);
        Assert.False(結果.ShouldFail);
    }

    /*
    {
      "責務": "設定の失敗基準と上書き後の実効基準を別々に保ちます。",
      "処理": ["同じ診断を基準だけ変えて実行します。", "元の設定と実効基準、失敗判定を確認します。"],
      "引数": {"上書き": "任意の実効基準です。", "実効基準": "結果が保持する基準です。", "失敗する": "期待する失敗判定です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(null, DesignDiagnosticSeverity.Warning, false)]
    [InlineData(DesignDiagnosticSeverity.Attention, DesignDiagnosticSeverity.Attention, true)]
    [InlineData(DesignDiagnosticSeverity.Danger, DesignDiagnosticSeverity.Danger, false)]
    public void 設定と実効失敗基準を区別する(
        DesignDiagnosticSeverity? 上書き,
        DesignDiagnosticSeverity 実効基準,
        bool 失敗する
    )
    {
        var 環境 = new 解析ユースケース検証環境();
        var 結果 = 環境.作る()
            .実行する(
                "virtual",
                失敗基準の上書き: 上書き,
                キャンセル通知: TestContext.Current.CancellationToken
            );
        Assert.Equal(実効基準, 結果.FailureThreshold);
        Assert.Equal(DesignDiagnosticSeverity.Warning, 結果.Configuration.FailureThreshold);
        Assert.Equal(失敗する, 結果.ShouldFail);
    }

    /*
    {
      "責務": "各依存の例外を変えずに伝え、後続段階を実行しません。",
      "処理": ["指定した段階で同じ例外を投げます。", "例外の同一性と実行済み段階を確認します。"],
      "引数": {"失敗段階": "0から始まる実行順の位置です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void 例外を変えず後続処理を停止する(int 失敗段階)
    {
        var 期待する例外 = new InvalidOperationException("dependency failure");
        var 環境 = new 解析ユースケース検証環境
        {
            段階の処理 = 段階 =>
            {
                if (段階 == 解析段階の順序[失敗段階])
                    throw 期待する例外;
            },
        };
        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            環境.作る().実行する("virtual", キャンセル通知: TestContext.Current.CancellationToken)
        );
        Assert.Same(期待する例外, 例外);
        Assert.Equal(解析段階の順序.Take(失敗段階 + 1), 環境.呼出順序);
    }

    /*
    {
      "責務": "開始前、入力後、読込後、解析中のキャンセルを伝播します。",
      "処理": ["指定段階で通知をキャンセルします。", "後続依存を呼ばず、元の通知を持つ例外になることを確認します。"],
      "引数": {"中断位置": "開始前、入力後、プロジェクト読込後、診断生成中の順です。", "呼出数": "中断までに呼ばれる段階数です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(3, 5)]
    public void キャンセル時に後続依存を呼ばない(int 中断位置, int 呼出数)
    {
        using var 通知元 = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        var 中断段階 = 中断位置 switch
        {
            1 => "入力",
            2 => "プロジェクト",
            _ => "診断",
        };
        var 環境 = new 解析ユースケース検証環境
        {
            段階の処理 = 段階 =>
            {
                if (段階 == 中断段階)
                    通知元.Cancel();
            },
        };
        if (中断位置 == 0)
            通知元.Cancel();

        var 例外 = Assert.Throws<OperationCanceledException>(() =>
            環境.作る().実行する("virtual", キャンセル通知: 通知元.Token)
        );
        Assert.Equal(通知元.Token, 例外.CancellationToken);
        Assert.Equal(解析段階の順序.Take(呼出数), 環境.呼出順序);
    }

    /*
    {
      "責務": "必須の依存が欠けた状態でユースケースを生成しません。",
      "処理": ["4つの依存を1つずつnullにして構築します。", "該当する引数が説明されることを確認します。"],
      "引数": {"依存番号": "0から始まるコンストラクター引数の位置です。", "引数名": "欠けた依存の名前です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(0, "入力を読み込む")]
    [InlineData(1, "規則群を取得する")]
    [InlineData(2, "読み込み器を生成する")]
    [InlineData(3, "診断群を生成する")]
    public void nullの依存を拒否する(int 依存番号, string 引数名)
    {
        var 環境 = new 解析ユースケース検証環境();
        var 例外 = Assert.Throws<ArgumentNullException>(() =>
            new 解析ユースケース(
                依存番号 == 0 ? null! : 環境.入力を読み込む,
                依存番号 == 1 ? null! : 環境.規則群を取得する,
                依存番号 == 2 ? null! : 環境.読み込み器を生成する,
                依存番号 == 3 ? null! : 環境.診断群を生成する
            )
        );
        Assert.Equal(引数名, 例外.ParamName);
    }
}
