using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 拡張規則スモークテスト
{
    public static void 実行する()
    {
        未使用の単一実装抽象を注意として報告する();
        不変条件迂回を危険として検出する();
        外部状態操作を警告する();
        不自然な実装継承を警告する();
        親の契約を無効にする実装を警告する();
        実装再利用のための継承を注意として報告する();
        外部振る舞いを持つ貧血オブジェクトを警告する();
        シリアル化データ保持型を許可する();
        データ契約型を許可する();
        命名だけでドメイン振る舞いを除外しない();
        マッパー投影を貧血オブジェクトと判定しない();
        無関係な依存群は警告になる();
        補助処理で結ばれた依存群は許可する();
        共通抽象を持つ依存群は許可する();
        オブジェクトを深く辿る処理は注意する();
        取得設定のみオブジェクトを注意として検出する();
        メッセージデータ保持型を許可する();
    }

    private static void 未使用の単一実装抽象を注意として報告する()
    {
        const string ソース = """
            internal interface IUnused
            {
                void Run();
            }

            internal sealed class OnlyImplementation : IUnused
            {
                public void Run() { }
            }
            """;

        単一診断を確認する(
            new 不要な抽象規則(),
            ソース,
            "OOP003",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void 不変条件迂回を危険として検出する()
    {
        const string ソース = """
            using System;

            internal sealed class Account
            {
                public int Balance { get; set; }

                public void ChangeBalance(int value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    Balance = value;
                }
            }
            """;

        単一診断を確認する(
            new 不変条件迂回規則(),
            ソース,
            "OOP107",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 外部状態操作を警告する()
    {
        const string ソース = """
            internal sealed class Player
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Hp { get; set; }
            }

            internal sealed class PlayerEditor
            {
                public void Reset(Player player)
                {
                    player.X = 0;
                    player.Y = 0;
                    player.Hp = 100;
                }
            }
            """;

        単一診断を確認する(
            new 外部状態操作過剰規則(),
            ソース,
            "OOP108",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 不自然な実装継承を警告する()
    {
        const string ソース = """
            internal class Base
            {
                public void Start() { }
                public int Read() => 1;
            }

            internal sealed class Child : Base
            {
                public new void Start() { }
                public new int Read() => 2;
            }
            """;

        単一診断を確認する(
            new 疑わしい継承関係規則(),
            ソース,
            "OOP301",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 親の契約を無効にする実装を警告する()
    {
        const string ソース = """
            internal class Base
            {
                public virtual void Start() => System.GC.KeepAlive(this);
                public virtual object? Read() => new object();
            }

            internal sealed class Child : Base
            {
                public override void Start() { }
                public override object? Read() => null;
            }
            """;

        単一診断を確認する(
            new 親契約未使用規則(),
            ソース,
            "OOP302",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 実装再利用のための継承を注意として報告する()
    {
        const string ソース = """
            internal class ReusableBase
            {
                protected int Left;
                protected int Right;
            }

            internal sealed class Calculator : ReusableBase
            {
                public int Sum() => Left + Right;
            }
            """;

        単一診断を確認する(
            new コンポジション候補規則(),
            ソース,
            "OOP307",
            DesignDiagnosticSeverity.Attention
        );
    }

    /*
    {
      "責務": "複数の状態を使う更新が外部へ置かれたケースを検証します。",
      "処理": ["外部更新のあるソースを解析します。", "OOP402の警告を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    private static void 外部振る舞いを持つ貧血オブジェクトを警告する()
    {
        const string ソース = """
            internal sealed class Position
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
            }

            internal sealed class PositionService
            {
                public int Advance(Position position)
                {
                    return position.X += position.Y + position.Z;
                }
            }
            """;

        単一診断を確認する(
            new 貧血オブジェクト規則(),
            ソース,
            "OOP402",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void シリアル化データ保持型を許可する()
    {
        const string ソース = """
            [System.Serializable]
            internal sealed class Snapshot
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
                public int Version { get; set; }
            }

            internal sealed class SnapshotService
            {
                public int Sum(Snapshot snapshot)
                {
                    return snapshot.X + snapshot.Y + snapshot.Z;
                }
            }
            """;

        診断がないことを確認する(new 貧血オブジェクト規則(), ソース, "OOP402");
        診断がないことを確認する(new 取得設定のみオブジェクト規則(), ソース, "OOP405");
    }

    private static void データ契約型を許可する()
    {
        const string ソース = """
            [System.Runtime.Serialization.DataContract]
            internal sealed class Snapshot
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
                public int Version { get; set; }
            }

            internal sealed class SnapshotService
            {
                public int Sum(Snapshot snapshot)
                {
                    return snapshot.X + snapshot.Y + snapshot.Z;
                }
            }
            """;

        診断がないことを確認する(new 貧血オブジェクト規則(), ソース, "OOP402");
        診断がないことを確認する(new 取得設定のみオブジェクト規則(), ソース, "OOP405");
    }

    private static void 命名だけでドメイン振る舞いを除外しない()
    {
        const string ソース = """
            internal sealed class AccountModel
            {
                public int Balance { get; set; }
                public int Limit { get; set; }
                public int Pending { get; set; }
                public int Version { get; set; }

                public void Apply(int amount)
                {
                    Balance += amount;
                }
            }
            """;

        単一診断を確認する(
            new 取得設定のみオブジェクト規則(),
            ソース,
            "OOP405",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void マッパー投影を貧血オブジェクトと判定しない()
    {
        const string ソース = """
            internal sealed class Person
            {
                public string Name { get; set; } = string.Empty;
                public int Age { get; set; }
                public string City { get; set; } = string.Empty;
            }

            internal sealed class PersonMapper
            {
                public string Map(Person person)
                {
                    return $"{person.Name}:{person.Age}:{person.City}";
                }
            }
            """;

        診断がないことを確認する(new 貧血オブジェクト規則(), ソース, "OOP402");
    }

    private static void 無関係な依存群は警告になる()
    {
        const string ソース = """
            internal sealed class A { public int Read() => 1; }
            internal sealed class B { public int Read() => 1; }
            internal sealed class C { public int Read() => 1; }
            internal sealed class D { public int Read() => 1; }
            internal sealed class E { public int Read() => 1; }
            internal sealed class F { public int Read() => 1; }

            internal sealed class Coordinator
            {
                private readonly A _a = new();
                private readonly B _b = new();
                private readonly C _c = new();
                private readonly D _d = new();
                private readonly E _e = new();
                private readonly F _f = new();

                public int First() => _a.Read() + _b.Read();
                public int Second() => _c.Read() + _d.Read();
                public int Third() => _e.Read() + _f.Read();
            }
            """;

        単一診断を確認する(
            new 過剰無関係依存規則(),
            ソース,
            "OOP403",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 補助処理で結ばれた依存群は許可する()
    {
        const string ソース = """
            internal sealed class A { public int Read() => 1; }
            internal sealed class B { public int Read() => 1; }
            internal sealed class C { public int Read() => 1; }
            internal sealed class D { public int Read() => 1; }
            internal sealed class E { public int Read() => 1; }
            internal sealed class F { public int Read() => 1; }

            internal sealed class Coordinator
            {
                private readonly A _a = new();
                private readonly B _b = new();
                private readonly C _c = new();
                private readonly D _d = new();
                private readonly E _e = new();
                private readonly F _f = new();

                public int Run() => First() + Second() + Third();

                private int First() => _a.Read() + _b.Read();
                private int Second() => _c.Read() + _d.Read();
                private int Third() => _e.Read() + _f.Read();
            }
            """;

        診断がないことを確認する(new 過剰無関係依存規則(), ソース, "OOP403");
    }

    private static void 共通抽象を持つ依存群は許可する()
    {
        const string ソース = """
            internal interface IReader
            {
                int Read();
            }

            internal sealed class A : IReader { public int Read() => 1; }
            internal sealed class B : IReader { public int Read() => 1; }
            internal sealed class C : IReader { public int Read() => 1; }
            internal sealed class D : IReader { public int Read() => 1; }
            internal sealed class E : IReader { public int Read() => 1; }
            internal sealed class F : IReader { public int Read() => 1; }

            internal sealed class Coordinator
            {
                private readonly A _a = new();
                private readonly B _b = new();
                private readonly C _c = new();
                private readonly D _d = new();
                private readonly E _e = new();
                private readonly F _f = new();

                public int First() => _a.Read() + _b.Read();
                public int Second() => _c.Read() + _d.Read();
                public int Third() => _e.Read() + _f.Read();
            }
            """;

        診断がないことを確認する(new 過剰無関係依存規則(), ソース, "OOP403");
    }

    private static void オブジェクトを深く辿る処理は注意する()
    {
        const string ソース = """
            internal sealed class Country
            {
                public void Print() { }
            }

            internal sealed class Address
            {
                public Country Country { get; } = new();
            }

            internal sealed class Customer
            {
                public Address Address { get; } = new();
            }

            internal sealed class Order
            {
                public Customer Customer { get; } = new();
            }

            internal sealed class Handler
            {
                public void Handle(Order order)
                {
                    order.Customer.Address.Country.Print();
                }
            }
            """;

        単一診断を確認する(
            new 過剰オブジェクト辿り規則(),
            ソース,
            "OOP404",
            DesignDiagnosticSeverity.Attention
        );
    }

    /*
    {
      "責務": "状態更新がある型で、単純な状態公開への偏りを検証します。",
      "処理": ["更新メソッドと4つのプロパティを持つ型を解析します。", "OOP405の注意を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    private static void 取得設定のみオブジェクトを注意として検出する()
    {
        const string ソース = """
            internal sealed class Person
            {
                public string Name { get; set; } = string.Empty;
                public int Age { get; set; }
                public string City { get; set; } = string.Empty;
                public string Country { get; set; } = string.Empty;
                public void HaveBirthday() => Age++;
            }
            """;

        単一診断を確認する(
            new 取得設定のみオブジェクト規則(),
            ソース,
            "OOP405",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void メッセージデータ保持型を許可する()
    {
        const string ソース = """
            internal sealed class UserCreatedMessage
            {
                public string UserId { get; set; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
                public long Timestamp { get; set; }
            }
            """;

        診断がないことを確認する(new 取得設定のみオブジェクト規則(), ソース, "OOP405");
    }

    private static void 単一診断を確認する(
        I解析規則 規則,
        string ソース,
        string 規則ID,
        DesignDiagnosticSeverity 重要度
    )
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length != 1 || 診断一覧[0].Rule.Id != 規則ID || 診断一覧[0].Severity != 重要度)
        {
            var 検出内容 = string.Join(
                ", ",
                診断一覧.Select(項目 => $"{項目.Rule.Id}:{項目.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {規則ID}:{重要度} diagnostic, found [{検出内容}]."
            );
        }
    }

    private static void 診断がないことを確認する(I解析規則 規則, string ソース, string 規則ID)
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length == 0)
        {
            return;
        }

        var 検出内容 = string.Join(", ", 診断一覧.Select(項目 => 項目.Rule.Id));
        throw new InvalidOperationException(
            $"Expected no {規則ID} diagnostic, found [{検出内容}]."
        );
    }
}
