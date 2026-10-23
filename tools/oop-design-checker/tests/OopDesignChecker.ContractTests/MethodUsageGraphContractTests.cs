using Microsoft.CodeAnalysis;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {"メソッド利用グラフ契約テスト": "共有グラフの到達可能性、対象選別、既存の宣言・呼出範囲を検証します。"},
  "フィールド": {"探索用ソース": "分岐・多段helper・循環・自己再帰を含むコンパイル可能なソースです。"}
}
*/
public sealed class メソッド利用グラフ契約テスト
{
    private const string 探索用ソース = """
        internal sealed class Sample
        {
            private int A;
            private int B;
            private int P { get; set; }
            public int Start() => Left() + Right();
            private int Left() => Leaf();
            private int Right() => Leaf() + B;
            private int Leaf() => A + P;
            private int Cycle() => Loop() + A;
            private int Loop() => Cycle() + B;
            private int Self() => Self() + P;
            private int Empty() => 42;
            public int Other(Sample other) => other.Leaf();
        }
        """;

    /*
    {
      "責務": {"到達可能なメンバーだけを重複なく集計する": "多段・合流・循環・自己再帰を辿り、空の利用は結果から除外します。"},
      "処理": ["共有グラフの状態利用を集計します。", "指定メソッドの利用を名前の集合で確認します。"],
      "引数": {"メソッド名": "集計の起点です。", "期待名": "名前順に並べた期待メンバーです。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("Start", "A,B,P")]
    [InlineData("Left", "A,P")]
    [InlineData("Right", "A,B,P")]
    [InlineData("Leaf", "A,P")]
    [InlineData("Cycle", "A,B")]
    [InlineData("Loop", "A,B")]
    [InlineData("Self", "P")]
    [InlineData("Empty", "")]
    [InlineData("Other", "A,P")]
    public void 到達可能なメンバーだけを重複なく集計する(string メソッド名, string 期待名)
    {
        var 環境 = new メソッド利用検証環境(探索用ソース);
        var 結果 = 環境.グラフ.有効利用を集計する<ISymbol>(状態メンバーである);
        var 利用 = 結果.Where(項目 => 項目.Key.Name == メソッド名).SelectMany(項目 => 項目.Value);
        Assert.Equal(
            期待名,
            string.Join(",", 利用.Select(記号 => 記号.Name).Order(StringComparer.Ordinal))
        );
        Assert.Equal(期待名.Length > 0, 結果.Keys.Any(メソッド => メソッド.Name == メソッド名));
    }

    /*
    {
      "責務": {"選別と返却集合の変更は次の集計へ影響しない": "状態と依存の異なる選別を独立して適用できることを確認します。"},
      "処理": ["特定フィールドだけを集計して返却集合を消去します。", "プロパティだけの集計と再集計が正しいことを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 選別と返却集合の変更は次の集計へ影響しない()
    {
        var 環境 = new メソッド利用検証環境(探索用ソース);
        var フィールド利用 = 環境.グラフ.有効利用を集計する<IFieldSymbol>(記号 => 記号.Name == "A");
        var 起点 = フィールド利用.Keys.Single(記号 => 記号.Name == "Start");
        Assert.Equal("A", Assert.Single(フィールド利用[起点]).Name);
        フィールド利用[起点].Clear();
        var プロパティ利用 = 環境.グラフ.有効利用を集計する<IPropertySymbol>(_ => true);
        Assert.Equal("P", Assert.Single(プロパティ利用[起点]).Name);
        var 再集計 = 環境.グラフ.有効利用を集計する<IFieldSymbol>(記号 => 記号.Name == "A");
        Assert.Equal("A", Assert.Single(再集計[起点]).Name);
    }

    /*
    {
      "責務": {"staticと別型とアクセサの処理を展開しない": "通常インスタンスメソッド以外の宣言と呼出先を除外します。"},
      "処理": ["継承・外部呼出・アクセサを含むソースを解析します。", "呼出元にはプロパティ自体の参照だけが残ることを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void staticと別型とアクセサの処理を展開しない()
    {
        const string ソース = """
            internal class Parent { protected int Base() => 1; }
            internal sealed class External { public int Read() => 1; }
            internal sealed class Sample : Parent
            {
                private int A;
                private int P => A;
                public Sample() { A = 1; }
                private static int Static() => 1;
                public int Start() => Base() + Static() + new External().Read() + P;
            }
            """;
        var 環境 = new メソッド利用検証環境(ソース);
        var 結果 = 環境.グラフ.有効利用を集計する<ISymbol>(状態メンバーである);
        var 起点 = Assert.Single(結果);
        Assert.Equal("Start", 起点.Key.Name);
        Assert.Equal("P", Assert.Single(起点.Value).Name);
    }

    /*
    {
      "責務": {"オーバーロードを記号で区別する": "同名メソッドの別シグネチャを同じ探索先として扱わないことを確認します。"},
      "処理": ["異なるフィールドを使う同名メソッドを解析します。", "呼出元は選ばれたシグネチャの利用だけを持つことを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void オーバーロードを記号で区別する()
    {
        var 環境 = new メソッド利用検証環境(
            """
            internal sealed class Sample
            {
                private int A; private int B;
                private int Read(int value) => A;
                private int Read(string value) => B;
                public int Start() => Read(1);
            }
            """
        );
        var 結果 = 環境.グラフ.有効利用を集計する<IFieldSymbol>(_ => true);
        Assert.Equal(2, 結果.Keys.Count(記号 => 記号.Name == "Read"));
        Assert.Equal("A", Assert.Single(結果.Single(項目 => 項目.Key.Name == "Start").Value).Name);
    }

    /*
    {
      "責務": {"別のpartial宣言へ探索範囲を広げない": "既存の宣言単位の解析範囲を維持し、別宣言のhelperを自動で補いません。"},
      "処理": ["helperを別partial宣言に置きます。", "最初の宣言の起点へ利用を展開しないことを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 別のpartial宣言へ探索範囲を広げない()
    {
        var 環境 = new メソッド利用検証環境(
            """
            internal sealed partial class Sample { public int Start() => Helper(); }
            internal sealed partial class Sample { private int A; private int Helper() => A; }
            """
        );
        Assert.Empty(環境.グラフ.有効利用を集計する<IFieldSymbol>(_ => true));
    }

    /*
    {
      "責務": {"状態メンバーである": "状態フィールドとプロパティをテストの集計対象として選びます。"},
      "処理": ["static以外のフィールドまたはプロパティかを判定します。"],
      "引数": {"記号": "参照先の記号です。"},
      "戻り値": "状態メンバーならtrueです。"
    }
    */
    private static bool 状態メンバーである(ISymbol 記号) =>
        記号 is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false };
}
