using OopDesignChecker.Core;
using OopDesignChecker.Rules;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {"メソッド利用規則回帰テスト": "共有化後もOOP401の状態閾値とOOP403の依存選別・接続条件を維持します。"}
}
*/
public sealed class メソッド利用規則回帰テスト
{
    /*
    {
      "責務": {"OOP401の状態とメソッド数の閾値を維持する": "循環helperを通る状態のまとまりと、その結合を規則として検証します。"},
      "処理": ["二つの状態群と循環するメソッド群を生成します。", "状態の種類、数、メソッド数、結合の有無に応じた診断を確認します。"],
      "引数": {"状態数": "各まとまりの状態数です。", "メソッド数": "各まとまりのメソッド数です。", "プロパティ": "状態をプロパティで表す場合にtrueです。", "結合": "両群を使うhelper呼出を追加する場合にtrueです。", "警告する": "診断が必要かの期待値です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(3, 3, false, false, true)]
    [InlineData(3, 3, true, false, true)]
    [InlineData(3, 3, false, true, false)]
    [InlineData(3, 3, true, true, false)]
    [InlineData(2, 3, false, false, false)]
    [InlineData(3, 2, false, false, false)]
    [InlineData(4, 4, false, false, true)]
    public void OOP401の状態とメソッド数の閾値を維持する(
        int 状態数,
        int メソッド数,
        bool プロパティ,
        bool 結合,
        bool 警告する
    )
    {
        var 左群 = 状態群を作る("Left", 状態数, メソッド数, プロパティ);
        var 右群 = 状態群を作る("Right", 状態数, メソッド数, プロパティ);
        var 結合メソッド = 結合 ? "public int Run() => Left0() + Right0();" : "";
        var 環境 = new メソッド利用検証環境(
            $"internal sealed class Sample {{ {左群} {右群} {結合メソッド} }}"
        );
        診断を確認する(環境.規則を解析する(new 複数オブジェクト混在規則()), "OOP401", 警告する);
    }

    /*
    {
      "責務": {"OOP403の独立依存群と共通抽象の接続を維持する": "依存数の閾値、helper経由の結合、共通インターフェースの結合を検証します。"},
      "処理": ["依存型と二段のhelper呼出を持つ対象を生成します。", "規則固有の接続と閾値に応じた診断を確認します。"],
      "引数": {"群数": "独立した依存群の数です。", "群内依存数": "各群のフィールド数です。", "結合": "すべての群をhelper経由で呼ぶかを表します。", "共通抽象": "全依存型が同じインターフェースを実装するかを表します。", "警告する": "診断が必要かの期待値です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(3, 2, false, false, true)]
    [InlineData(3, 2, true, false, false)]
    [InlineData(3, 2, false, true, false)]
    [InlineData(2, 2, false, false, false)]
    [InlineData(6, 1, false, false, false)]
    public void OOP403の独立依存群と共通抽象の接続を維持する(
        int 群数,
        int 群内依存数,
        bool 結合,
        bool 共通抽象,
        bool 警告する
    )
    {
        var 依存数 = 群数 * 群内依存数;
        var 抽象名 = 共通抽象 ? ": IReader" : "";
        var 依存型群 = string.Join(
            "\n",
            Enumerable
                .Range(0, 依存数)
                .Select(番号 =>
                    $"internal sealed class D{番号} {抽象名} {{ public int Read() => 1; }}"
                )
        );
        var フィールド群 = string.Join(
            "\n",
            Enumerable
                .Range(0, 依存数)
                .Select(番号 => $"private readonly D{番号} _d{番号} = new();")
        );
        var メソッド群 = string.Join(
            "\n",
            Enumerable
                .Range(0, 群数)
                .Select(群 =>
                    $"public int Run{群}() => Helper{群}(); private int Helper{群}() => "
                    + string.Join(
                        " + ",
                        Enumerable
                            .Range(群 * 群内依存数, 群内依存数)
                            .Select(番号 => $"_d{番号}.Read()")
                    )
                    + ";"
                )
        );
        var 結合メソッド = 結合
            ? "public int All() => "
                + string.Join(" + ", Enumerable.Range(0, 群数).Select(群 => $"Run{群}()"))
                + ";"
            : "";
        var 環境 = new メソッド利用検証環境(
            $$"""
            internal interface IReader { int Read(); }
            {{依存型群}}
            internal sealed class Sample { {{フィールド群}} {{メソッド群}} {{結合メソッド}} }
            """
        );
        診断を確認する(環境.規則を解析する(new 過剰無関係依存規則()), "OOP403", 警告する);
    }

    /*
    {
      "責務": {"状態群を作る": "各メソッドが次のhelperを呼び、群内のすべての状態へ到達するソースを作ります。"},
      "処理": ["フィールドまたはプロパティを作ります。", "状態を分担して使う循環メソッドを作ります。"],
      "引数": {"接頭辞": "群を区別する名前です。", "状態数": "生成する状態数です。", "メソッド数": "循環するメソッド数です。", "プロパティ": "プロパティで状態を表すかを指定します。"},
      "戻り値": "Sampleのメンバーとして挿入するC#コードです。"
    }
    */
    private static string 状態群を作る(string 接頭辞, int 状態数, int メソッド数, bool プロパティ)
    {
        var 状態群 = string.Join(
            "\n",
            Enumerable
                .Range(0, 状態数)
                .Select(番号 =>
                    $"private int {接頭辞}State{番号}" + (プロパティ ? " { get; set; }" : ";")
                )
        );
        var メソッド群 = string.Join(
            "\n",
            Enumerable
                .Range(0, メソッド数)
                .Select(番号 =>
                {
                    var 参照群 = Enumerable
                        .Range(0, 状態数)
                        .Where(状態 => 状態 % メソッド数 == 番号)
                        .Select(状態 => $"{接頭辞}State{状態}");
                    var 呼出先 = $"{接頭辞}{(番号 + 1) % メソッド数}()";
                    return $"public int {接頭辞}{番号}() => "
                        + string.Join(" + ", 参照群.Prepend(呼出先))
                        + ";";
                })
        );
        return 状態群 + "\n" + メソッド群;
    }

    /*
    {
      "責務": {"診断を確認する": "診断の有無と既存の規則ID・重要度・対象記号を確認します。"},
      "処理": ["期待がない場合は診断なしを確認します。", "期待がある場合はSampleへの警告が一件だけあることを確認します。"],
      "引数": {"診断群": "生成された診断です。", "規則ID": "対象規則のIDです。", "警告する": "警告を期待する場合にtrueです。"},
      "戻り値": []
    }
    */
    private static void 診断を確認する(DesignDiagnostic[] 診断群, string 規則ID, bool 警告する)
    {
        if (!警告する)
        {
            Assert.Empty(診断群);
            return;
        }

        var 診断 = Assert.Single(診断群);
        Assert.Equal(規則ID, 診断.Rule.Id);
        Assert.Equal(DesignDiagnosticSeverity.Warning, 診断.Severity);
        Assert.Equal("Sample", 診断.SymbolName);
    }
}
