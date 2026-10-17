using OopDesignChecker.Core;
using OopDesignChecker.Rules;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "データ型の誤検知対策が、他の設計規則の検出を弱めないことを検証します。",
  "フィールド": []
}
*/
public sealed partial class データ保持型精度契約テスト
{
    /*
    {
      "責務": "通常名の状態保持型に対する既存の危険・警告を維持します。",
      "処理": ["各規則の肯定ケースを解析します。", "指定された診断が残ることを確認します。"],
      "引数": {"規則ID": "検出を維持する規則です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("OOP106")]
    [InlineData("OOP107")]
    [InlineData("OOP108")]
    [InlineData("OOP404")]
    public void 他の規則の状態所有検査を維持する(string 規則ID)
    {
        I解析規則 規則 = 規則ID switch
        {
            "OOP106" => new カプセル化漏出規則(),
            "OOP107" => new 不変条件迂回規則(),
            "OOP108" => new 外部状態操作過剰規則(),
            "OOP404" => new 過剰オブジェクト辿り規則(),
            _ => throw new ArgumentOutOfRangeException(nameof(規則ID)),
        };
        const string ソース = """
            internal sealed class Person
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
                public int Version { get; set; }
                public System.Collections.Generic.List<int> Values { get; } = new();
                public void SetX(int value)
                {
                    if (value < 0) throw new System.ArgumentOutOfRangeException(nameof(value));
                    X = value;
                }
            }
            internal sealed class Writer
            {
                public void Update(Person value) { value.X = 1; value.Y = 2; value.Z = 3; }
            }
            internal sealed class Country { public void Print() { } }
            internal sealed class Address { public Country Country { get; } = new(); }
            internal sealed class Customer { public Address Address { get; } = new(); }
            internal sealed class Order { public Customer Customer { get; } = new(); }
            internal sealed class Handler { public void Handle(Order value) => value.Customer.Address.Country.Print(); }
            """;
        Assert.Contains(診断を取得する(ソース, 規則), 診断 => 診断.Rule.Id == 規則ID);
    }

    /*
    {
      "責務": "既存の明示的なデータ契約除外を維持します。",
      "処理": ["属性付きの状態保持型と更新処理を解析します。", "OOP402とOOP405を報告しないことを確認します。"],
      "引数": {"属性": "データ契約を明示する既知の属性です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("System.Serializable")]
    [InlineData("System.Runtime.Serialization.DataContract")]
    public void シリアル化の明示契約を維持する(string 属性)
    {
        var ソース = $$"""
            [{{属性}}]
            internal sealed class Person
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
                public int Version { get; set; }
            }
            internal sealed class Writer
            {
                public void Update(Person value) { value.X += value.Y + value.Z; }
            }
            """;
        Assert.Empty(診断を取得する(ソース, new 貧血オブジェクト規則()));
        Assert.Empty(診断を取得する(ソース, new 取得設定のみオブジェクト規則()));
    }
}
