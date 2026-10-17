using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "データの読み取りと、オブジェクトの状態更新を区別する契約を検証します。",
  "フィールド": []
}
*/
public sealed partial class データ保持型精度契約テスト
{
    /*
    {
      "責務": "名前やアクセサの種類によらず、読み取り専用の利用で貧血候補にしないことを検証します。",
      "処理": ["型と読み取り処理を作ります。", "OOP402とOOP405の診断がないことを確認します。"],
      "引数": {"型名": "データ型の名前です。", "アクセサ": "プロパティのアクセサです。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("Person", "get; init;")]
    [InlineData("Person", "get; set;")]
    [InlineData("Person", "get; private set;")]
    [InlineData("Person", "get;")]
    [InlineData("PersonModel", "get; set;")]
    public void 読み取り専用の利用を名前によらず許可する(string 型名, string アクセサ)
    {
        var ソース = $$"""
            internal sealed class {{型名}}
            {
                public int X { {{アクセサ}} }
                public int Y { {{アクセサ}} }
                public int Z { {{アクセサ}} }
                public int Version { {{アクセサ}} }
            }
            internal sealed class ReportBuilder
            {
                public string Build({{型名}} value) => $"{value.X}:{value.Y}:{value.Z}";
                public int Total({{型名}} value) => value.X + value.Y + value.Z;
            }
            """;
        Assert.Empty(診断を取得する(ソース, new 貧血オブジェクト規則()));
        Assert.Empty(診断を取得する(ソース, new 取得設定のみオブジェクト規則()));
    }

    /*
    {
      "責務": "オブジェクトの構築や複製を、既存状態の更新として扱わないことを検証します。",
      "処理": ["構築を行うデータ型を解析します。", "両規則の診断がないことを確認します。"],
      "引数": {"宣言": "クラスまたはレコードの宣言です。", "構築": "値を構築する処理です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("class", "new Person { X = 1, Y = 2, Z = 3, Version = 4 }")]
    [InlineData("record", "value with { X = 1, Y = 2, Z = 3, Version = 4 }")]
    [InlineData("record struct", "value with { X = 1, Y = 2, Z = 3, Version = 4 }")]
    public void 初期化とレコードの複製を許可する(string 宣言, string 構築)
    {
        var ソース = $$"""
            internal {{宣言}} Person
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
                public int Version { get; set; }
            }
            internal sealed class ReportBuilder
            {
                public Person Build(Person value) => {{構築}};
            }
            """;
        Assert.Empty(診断を取得する(ソース, new 貧血オブジェクト規則()));
        Assert.Empty(診断を取得する(ソース, new 取得設定のみオブジェクト規則()));
    }

    /*
    {
      "責務": "複数の状態を使って既存オブジェクトを更新する処理を引き続き検出します。",
      "処理": ["外部の状態更新処理を作ります。", "両規則が対象型を報告することを確認します。"],
      "引数": {"更新": "代入または増減を行う文です。", "型名": "状態の所有者の名前です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("value.X = value.Y + value.Z;", "Position")]
    [InlineData("value.X += value.Y + value.Z;", "Position")]
    [InlineData("value.X++; _ = value.Y + value.Z;", "Position")]
    [InlineData("++value.X; _ = value.Y + value.Z;", "Position")]
    [InlineData("(value.X, value.Y) = (value.Y, value.Z);", "Position")]
    [InlineData("value.X += value.Y + value.Z;", "PositionModel")]
    [InlineData("value.X += value.Y + value.Z;", "PositionDto")]
    public void 外部へ追い出された状態更新を検出する(string 更新, string 型名)
    {
        var ソース = $$"""
            internal sealed class {{型名}}
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
                public int Version { get; set; }
            }
            internal sealed class PositionService
            {
                public void Move({{型名}} value) { {{更新}} }
            }
            """;
        Assert.Equal(
            型名,
            Assert.Single(診断を取得する(ソース, new 貧血オブジェクト規則())).SymbolName
        );
        Assert.Equal(
            型名,
            Assert.Single(診断を取得する(ソース, new 取得設定のみオブジェクト規則())).SymbolName
        );
    }

    /*
    {
      "責務": "型自身が持つ状態更新を、名前を理由にデータ型へ分類しないことを検証します。",
      "処理": ["既存のAccountModel境界を解析します。", "OOP405が維持されることを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 型名だけでドメインの状態更新を除外しない()
    {
        const string ソース = """
            internal sealed class AccountModel
            {
                public int Balance { get; set; }
                public int Limit { get; set; }
                public int Pending { get; set; }
                public int Version { get; set; }
                public void Apply(int amount) => Balance += amount;
            }
            """;
        Assert.Single(診断を取得する(ソース, new 取得設定のみオブジェクト規則()));
    }

    /*
    {
      "責務": "別の引数の更新を、読み取り専用の引数へ誤って結び付けないことを検証します。",
      "処理": ["入力と出力を分けた投影を解析します。", "OOP402を報告しないことを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 読み取り元と書き込み先を区別する()
    {
        const string ソース = """
            internal sealed class Person
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
            }
            internal sealed class ReportBuilder
            {
                public void Build(Person input, Person output)
                {
                    output.X = input.X + input.Y + input.Z;
                }
            }
            """;
        Assert.Empty(診断を取得する(ソース, new 貧血オブジェクト規則()));
    }

    /*
    {
      "責務": "コンストラクター内で完結する値の設定を許可します。",
      "処理": ["コンストラクターで設定する型を解析します。", "OOP405の診断がないことを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void コンストラクターで完結する値を許可する()
    {
        const string ソース = """
            internal sealed class Person
            {
                public int X { get; private set; }
                public int Y { get; private set; }
                public int Z { get; private set; }
                public int Version { get; private set; }
                public Person(int x) { X = x; Y = x; Z = x; Version = 1; }
            }
            """;
        Assert.Empty(診断を取得する(ソース, new 取得設定のみオブジェクト規則()));
    }

    /*
    {
      "責務": "単一規則の結果を集約前に取り出し、規則間の抑制から検証を独立させます。",
      "処理": ["一時ソースを読み込みます。", "指定した規則を実行します。"],
      "引数": {"ソース": "解析するC#ソースです。", "規則": "検証する規則です。"},
      "戻り値": "指定した規則が生成した診断です。"
    }
    */
    private static DesignDiagnostic[] 診断を取得する(string ソース, I解析規則 規則)
    {
        using var 環境 = new 一時解析環境();
        var パス = 環境.ファイルを書く("Sample.cs", ソース);
        var 文脈 = new 解析文脈(new CSharpプロジェクト読み込み器().読み込む(パス));
        return 規則.解析する(文脈).ToArray();
    }
}
