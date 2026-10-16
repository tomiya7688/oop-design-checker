using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class オブジェクト辿り規則テスト
{
    public static void 実行する()
    {
        プロジェクトオブジェクトを深く辿る処理は注意する();
        フレームワーク型と値型の辿りは許可する();
        データ保持型の辿りは許可する();
        同じオブジェクト抽象の辿りは許可する();
    }

    private static void プロジェクトオブジェクトを深く辿る処理は注意する()
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

        単一診断を確認する(ソース);
    }

    private static void フレームワーク型と値型の辿りは許可する()
    {
        const string ソース = """
            internal sealed class Event
            {
                public System.DateTime CreatedAt { get; } = System.DateTime.UtcNow;
            }

            internal sealed class Handler
            {
                public string Handle(Event item)
                {
                    return item.CreatedAt.Date.TimeOfDay.TotalSeconds.ToString();
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void データ保持型の辿りは許可する()
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

            internal sealed class Profile
            {
                public Address Address { get; } = new();
            }

            internal sealed class Customer
            {
                public Profile Profile { get; } = new();
            }

            internal sealed class OrderDto
            {
                public Customer Customer { get; } = new();
            }

            internal sealed class Handler
            {
                public void Handle(OrderDto order)
                {
                    order.Customer.Profile.Address.Country.Print();
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void 同じオブジェクト抽象の辿りは許可する()
    {
        const string ソース = """
            internal interface INode
            {
                INode Next { get; }
                void Print();
            }

            internal sealed class Node : INode
            {
                public INode Next => this;
                public void Print() { }
            }

            internal sealed class Handler
            {
                public void Handle(Node node)
                {
                    node.Next.Next.Next.Print();
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void 単一診断を確認する(string ソース)
    {
        var 診断一覧 = 解析する(ソース);
        if (
            診断一覧.Length != 1
            || 診断一覧[0].Rule.Id != "OOP404"
            || 診断一覧[0].Severity != DesignDiagnosticSeverity.Attention
        )
        {
            throw new InvalidOperationException(
                $"Expected one OOP404:Attention diagnostic, found {診断一覧.Length}."
            );
        }
    }

    private static void 診断がないことを確認する(string ソース)
    {
        var 診断一覧 = 解析する(ソース);
        if (診断一覧.Length != 0)
        {
            throw new InvalidOperationException(
                $"Expected no OOP404 diagnostics, found {診断一覧.Length}."
            );
        }
    }

    private static DesignDiagnostic[] 解析する(string ソース) =>
        new 過剰オブジェクト辿り規則()
            .解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース))
            .Where(診断 => 診断.Rule.Id == "OOP404")
            .ToArray();
}
