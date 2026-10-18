using OopDesignChecker.Core;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "解析結果の診断所有権と公開record契約を検証します。",
  "フィールド": []
}
*/
public sealed class 解析結果契約テスト
{
    /*
    {
      "責務": "元の配列やリストの更新から結果を保護します。",
      "処理": ["結果を作成してから入力の要素を置換し、リストには要素も追加します。", "診断、失敗判定、ハッシュ値が変わらないことを確認します。"],
      "引数": {"配列を使う": "配列とListの両方の入力を検証します。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 入力コレクションの変更を反映しない(bool 配列を使う)
    {
        var 元の診断 = 診断を作る(DesignDiagnosticSeverity.Warning);
        IList<DesignDiagnostic> 入力 = 配列を使う
            ? new[] { 元の診断 }
            : new List<DesignDiagnostic> { 元の診断 };
        var 結果 = new CheckerRunResult(
            (IReadOnlyList<DesignDiagnostic>)入力,
            DesignDiagnosticSeverity.Danger,
            null
        );
        var 元のハッシュ値 = 結果.GetHashCode();

        // 入力の置換と増減は、既に返した結果へ影響してはいけません。
        入力[0] = 診断を作る(DesignDiagnosticSeverity.Danger);
        if (!配列を使う)
            入力.Add(診断を作る(DesignDiagnosticSeverity.Attention));

        Assert.Same(元の診断, Assert.Single(結果.Diagnostics));
        Assert.False(結果.ShouldFail);
        Assert.Equal(元のハッシュ値, 結果.GetHashCode());
    }

    /*
    {
      "責務": "読み取り専用インターフェースのキャストによる書き換えを防ぎます。",
      "処理": ["結果の一覧をIListへキャストし、置換と削除が拒否されることを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 結果の診断一覧を書き換えられない()
    {
        var 診断 = 診断を作る(DesignDiagnosticSeverity.Warning);
        var 結果 = new CheckerRunResult([診断], DesignDiagnosticSeverity.Warning, null);
        var 一覧 = Assert.IsAssignableFrom<IList<DesignDiagnostic>>(結果.Diagnostics);

        Assert.True(一覧.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => 一覧[0] = 診断);
        Assert.Throws<NotSupportedException>(() => 一覧.Clear());
        Assert.True(結果.ShouldFail);
    }

    /*
    {
      "責務": "空の結果とすべての重大度の組み合わせについて既存の失敗基準を維持します。",
      "処理": ["診断の有無と重大度を変え、基準以上の場合だけ失敗することを確認します。"],
      "引数": {"診断重大度": "nullなら空一覧を使用します。", "失敗基準": "結果が持つ失敗基準です。", "失敗する": "期待する判定です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(null, DesignDiagnosticSeverity.Attention, false)]
    [InlineData(null, DesignDiagnosticSeverity.Warning, false)]
    [InlineData(null, DesignDiagnosticSeverity.Danger, false)]
    [InlineData(DesignDiagnosticSeverity.Attention, DesignDiagnosticSeverity.Attention, true)]
    [InlineData(DesignDiagnosticSeverity.Attention, DesignDiagnosticSeverity.Warning, false)]
    [InlineData(DesignDiagnosticSeverity.Attention, DesignDiagnosticSeverity.Danger, false)]
    [InlineData(DesignDiagnosticSeverity.Warning, DesignDiagnosticSeverity.Attention, true)]
    [InlineData(DesignDiagnosticSeverity.Warning, DesignDiagnosticSeverity.Warning, true)]
    [InlineData(DesignDiagnosticSeverity.Warning, DesignDiagnosticSeverity.Danger, false)]
    [InlineData(DesignDiagnosticSeverity.Danger, DesignDiagnosticSeverity.Attention, true)]
    [InlineData(DesignDiagnosticSeverity.Danger, DesignDiagnosticSeverity.Warning, true)]
    [InlineData(DesignDiagnosticSeverity.Danger, DesignDiagnosticSeverity.Danger, true)]
    public void 重大度と失敗基準の比較を維持する(
        DesignDiagnosticSeverity? 診断重大度,
        DesignDiagnosticSeverity 失敗基準,
        bool 失敗する
    )
    {
        DesignDiagnostic[] 診断群 = 診断重大度 is { } 重大度 ? [診断を作る(重大度)] : [];
        var 結果 = new CheckerRunResult(診断群, 失敗基準, null);
        Assert.Equal(失敗する, 結果.ShouldFail);
    }

    /*
    {
      "責務": "名前付き引数、分解代入、record複製と等価性の互換性を確認します。",
      "処理": ["公開名で結果を構築して分解し、変更のないwith複製と比較します。", "基準を変更した複製だけ失敗判定が変わることを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 公開recordの操作を維持する()
    {
        var 結果 = new CheckerRunResult(
            Diagnostics: [診断を作る(DesignDiagnosticSeverity.Warning)],
            FailureThreshold: DesignDiagnosticSeverity.Danger,
            ConfigurationPath: "settings.json"
        );
        var (診断群, 基準, 設定パス) = 結果;
        var 複製 = 結果 with { };
        var 基準変更 = 結果 with { FailureThreshold = DesignDiagnosticSeverity.Warning };

        Assert.Same(結果.Diagnostics, 診断群);
        Assert.Equal(DesignDiagnosticSeverity.Danger, 基準);
        Assert.Equal("settings.json", 設定パス);
        Assert.True(結果 == 複製);
        Assert.True(結果.Equals(複製));
        Assert.Equal(結果.GetHashCode(), 複製.GetHashCode());
        Assert.Same(結果.Diagnostics, 複製.Diagnostics);
        Assert.Same(結果.Configuration, 複製.Configuration);
        Assert.False(結果.ShouldFail);
        Assert.True(基準変更.ShouldFail);
        Assert.NotEqual(結果, 基準変更);
    }

    /*
    {
      "責務": "initとwithによる診断の差し替えでも入力をコピーします。",
      "処理": ["一覧を差し替えた後に元リストを空にします。", "差し替え前後の結果がそれぞれの診断を保つことを確認します。"],
      "引数": {"withを使う": "with式とオブジェクト初期化子の両方を検証します。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 診断の差し替え時にもコピーする(bool withを使う)
    {
        var 元の結果 = new CheckerRunResult([], DesignDiagnosticSeverity.Warning, null);
        var 入力 = new List<DesignDiagnostic> { 診断を作る(DesignDiagnosticSeverity.Danger) };
        var 差替結果 = withを使う
            ? 元の結果 with
            {
                Diagnostics = 入力,
            }
            : new CheckerRunResult([], DesignDiagnosticSeverity.Warning, null)
            {
                Diagnostics = 入力,
            };
        入力.Clear();

        Assert.Empty(元の結果.Diagnostics);
        Assert.False(元の結果.ShouldFail);
        Assert.Single(差替結果.Diagnostics);
        Assert.True(差替結果.ShouldFail);
    }

    /*
    {
      "責務": "診断一覧の比較を内容比較へ変更しないことを明示します。",
      "処理": ["同じ入力から個別に構築した結果が独立した一覧を持つことを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 個別に構築した一覧は参照で比較する()
    {
        DesignDiagnostic[] 入力 = [診断を作る(DesignDiagnosticSeverity.Warning)];
        var 結果 = new CheckerRunResult(入力, DesignDiagnosticSeverity.Danger, null);
        var 別の一覧 = 結果 with { Diagnostics = 入力 };
        Assert.NotSame(結果.Diagnostics, 別の一覧.Diagnostics);
        Assert.NotEqual(結果, 別の一覧);
    }

    /*
    {
      "責務": "テスト用の不変な診断を構築します。",
      "処理": ["重大度以外を共通の値にした診断を返します。"],
      "引数": {"重大度": "判定に使用する重大度です。"},
      "戻り値": "規則と位置を含む診断です。"
    }
    */
    private static DesignDiagnostic 診断を作る(DesignDiagnosticSeverity 重大度) =>
        new(
            new RuleDescriptor("OOP106", "公開フィールド", 重大度),
            重大度,
            "診断",
            null,
            new("Sample.cs", 1, 1)
        );
}
