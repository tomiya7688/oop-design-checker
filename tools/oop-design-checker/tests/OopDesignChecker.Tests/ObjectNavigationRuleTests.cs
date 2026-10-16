using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class オブジェクト辿り規則テスト
{
    public static void Run()
    {
        プロジェクトオブジェクトを深く辿る処理は注意する();
        フレームワーク型と値型の辿りは許可する();
        データ保持型の辿りは許可する();
        同じオブジェクト抽象の辿りは許可する();
    }

    private static void プロジェクトオブジェクトを深く辿る処理は注意する()
    {
        const string source = """
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

        AssertSingle(source);
    }

    private static void フレームワーク型と値型の辿りは許可する()
    {
        const string source = """
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

        AssertNone(source);
    }

    private static void データ保持型の辿りは許可する()
    {
        const string source = """
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

        AssertNone(source);
    }

    private static void 同じオブジェクト抽象の辿りは許可する()
    {
        const string source = """
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

        AssertNone(source);
    }

    private static void AssertSingle(string source)
    {
        var diagnostics = 解析する(source);
        if (
            diagnostics.Length != 1
            || diagnostics[0].Rule.Id != "OOP404"
            || diagnostics[0].Severity != DesignDiagnosticSeverity.Attention
        )
        {
            throw new InvalidOperationException(
                $"Expected one OOP404:Attention diagnostic, found {diagnostics.Length}."
            );
        }
    }

    private static void AssertNone(string source)
    {
        var diagnostics = 解析する(source);
        if (diagnostics.Length != 0)
        {
            throw new InvalidOperationException(
                $"Expected no OOP404 diagnostics, found {diagnostics.Length}."
            );
        }
    }

    private static DesignDiagnostic[] 解析する(string source) =>
        new 過剰オブジェクト辿り規則()
            .解析する(TestProjectFactory.解析文脈を作る(source))
            .Where(diagnostic => diagnostic.Rule.Id == "OOP404")
            .ToArray();
}
