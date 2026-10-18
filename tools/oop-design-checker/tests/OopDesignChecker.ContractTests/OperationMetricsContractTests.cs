using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "OOP201が使う7指標の意味を固定し、責務整理による測定値の変化を検出します。",
  "フィールド": []
}
*/
public sealed class 処理量指標契約テスト
{
    /*
    {
      "責務": "代表的な構文について、すべての処理量指標を既知の値と比較します。",
      "処理": ["メソッドをコンパイルします。", "7指標を固定の期待値と比較します。"],
      "引数": {
        "メソッドソース": "測定対象のメソッド宣言です。",
        "期待値": "行数、文数、循環的複雑度、最大ネスト深度、ローカル変数数、状態メンバー数、トップレベル文数の順です。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [MemberData(nameof(測定例を列挙する))]
    public void 各指標の測定値を維持する(string メソッドソース, int[] 期待値)
    {
        var 指標 = 指標を取得する(メソッドソース);
        int[] 測定値 =
        [
            指標.行数,
            指標.文数,
            指標.循環的複雑度,
            指標.最大ネスト深度,
            指標.ローカル変数数,
            指標.状態メンバー数,
            指標.トップレベル文数,
        ];
        Assert.Equal(期待値, 測定値);
    }

    /*
    {
      "責務": "既存のスコア境界をまたぐ直線処理の診断と説明文を固定します。",
      "処理": ["境界前後の文数でメソッドを作ります。", "規則の判定と診断の測定値を確認します。"],
      "引数": {"加算文数": "直線的な更新を繰り返す数です。", "警告する": "既存基準で警告される場合にtrueです。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(114, false)]
    [InlineData(115, true)]
    public void 警告の境界と診断文を維持する(int 加算文数, bool 警告する)
    {
        var 宣言 = string.Join(
            "\n",
            new[] { "public int Run()", "{", "var value = 0;" }
                .Concat(Enumerable.Repeat("value += 1;", 加算文数))
                .Concat(["return value;", "}"])
        );
        var 診断群 = new 過大主処理規則().解析する(解析文脈を作る(宣言)).ToArray();
        if (!警告する)
        {
            Assert.Empty(診断群);
            return;
        }

        var 診断 = Assert.Single(診断群);
        Assert.Equal("OOP201", 診断.Rule.Id);
        Assert.Equal(DesignDiagnosticSeverity.Warning, 診断.Severity);
        Assert.Equal("Sample.Run()", 診断.SymbolName);
        Assert.Equal(
            "This primary operation is structurally large: 120 lines, 118 statements, complexity 1, nesting 0. Split meaningful processing phases into methods.",
            診断.Message
        );
    }

    /*
    {
      "責務": "構文別の固定例を、LFとCRLFの両方で供給します。",
      "処理": ["各指標を手で数えた例を定義します。", "改行形式を変えて列挙します。"],
      "引数": [],
      "戻り値": "メソッド宣言と7指標の期待値の組です。"
    }
    */
    public static IEnumerable<object[]> 測定例を列挙する()
    {
        (string メソッド, int[] 指標)[] 測定例 =
        [
            ("public void Run() { }", [1, 1, 1, 0, 0, 0, 0]),
            ("public int Run() => _value + _value;", [1, 0, 1, 0, 0, 1, 1]),
            ("public abstract int Run();", [1, 0, 1, 0, 0, 0, 0]),
            ("public int Run(int? value) => value ?? 0;", [1, 0, 1, 0, 0, 0, 1]),
            (
                "public int Run() { int x = _value; int y = base.Inherited; _value = x + y; return _value + Count; }",
                [1, 5, 1, 0, 2, 2, 4]
            ),
            (
                "public void Run() { for (int i = 0; i < 2; i++) { if (i == 0) { _value++; } } foreach (var x in new[] { 1 }) { _value += x; } while (_value < 10) { _value++; } do { _value--; } while (_value > 5); }",
                [1, 15, 6, 2, 2, 1, 4]
            ),
            (
                "public int Run(int value) { try { lock (this) { switch (value) { case 0: return 1; case > 0: return 2; default: break; } } } catch (System.Exception) { return -1; } return 0; }",
                [1, 12, 4, 3, 0, 0, 2]
            ),
            (
                "public int Run(int value) => value switch { 0 => 1, _ => value > 0 && value < 5 || value == 9 ? 1 : 2 };",
                [1, 0, 6, 0, 0, 0, 1]
            ),
            (
                "public void Run() { using (var stream = new System.IO.MemoryStream()) { _value = 1; } }",
                [1, 4, 1, 1, 1, 1, 1]
            ),
            (
                """
                public int Run()
                {
                    int Local() { if (Count > 0) return Count; return 0; }
                    System.Func<int> read = () => { if (Count > 0) return Inherited; return _value; };
                    System.Func<int, int> select = n => n > 0 ? n : 0;
                    System.Func<int> other = delegate { if (Count > 0) return Inherited; return _value; };
                    return read() + select(Local()) + other();
                }
                """,
                [8, 5, 1, 0, 3, 0, 4]
            ),
        ];

        // 改行コードは行数の意味を変えません。入れ子の処理は行範囲だけに含まれます。
        foreach (var (メソッド, 指標) in 測定例)
        {
            yield return [メソッド.ReplaceLineEndings("\n"), 指標];
            yield return [メソッド.ReplaceLineEndings("\r\n"), 指標];
        }
    }

    /*
    {
      "責務": "単一メソッドをメモリ上でコンパイルし、実際の集計器から指標を得ます。",
      "処理": ["状態メンバーと継承元を含むソースを作ります。", "コンパイルの成功を確認して対象メソッドを測定します。"],
      "引数": {"メソッドソース": "Runという名前のメソッド宣言です。"},
      "戻り値": "解析器が算出した7指標です。"
    }
    */
    private static 処理量指標 指標を取得する(string メソッドソース)
    {
        var プロジェクト = 解析文脈を作る(メソッドソース).対象プロジェクト;
        var 構文木 = プロジェクト.構文木群.Single();
        var 意味モデル = プロジェクト.意味モデルを取得する(構文木);
        var 宣言 = 構文木.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var 記号 = Assert.IsAssignableFrom<IMethodSymbol>(意味モデル.GetDeclaredSymbol(宣言));
        return 処理量指標計算器.計算する(宣言, 記号, 意味モデル);
    }

    /*
    {
      "責務": "固定例を、外部プロジェクトの復元に依存しない解析文脈へ変換します。",
      "処理": ["継承元と状態を含むソースを作ります。", "コンパイルの成功を確認して意味モデルを保持します。"],
      "引数": {"メソッドソース": "検証するメソッド宣言です。"},
      "戻り値": "メモリ上の単一プロジェクトの解析文脈です。"
    }
    */
    private static 解析文脈 解析文脈を作る(string メソッドソース)
    {
        var ソース = $$"""
            internal class Parent { protected int Inherited { get; set; } }
            internal abstract class Sample : Parent
            {
                private int _value;
                private static int Count;
                {{メソッドソース}}
            }
            """;
        var 構文木 = CSharpSyntaxTree.ParseText(ソース, path: "Operation.cs");
        var コンパイル = CSharpCompilation.Create(
            "OperationMetricsTests",
            [構文木],
            メタデータ参照提供器.プラットフォーム参照群を作る(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        Assert.DoesNotContain(
            コンパイル.GetDiagnostics(),
            診断 => 診断.Severity == DiagnosticSeverity.Error
        );
        var プロジェクト = new 解析対象プロジェクト(
            ".",
            true,
            コンパイル,
            new Dictionary<SyntaxTree, SemanticModel>
            {
                [構文木] = コンパイル.GetSemanticModel(構文木),
            }
        );
        return new 解析文脈(プロジェクト);
    }
}
