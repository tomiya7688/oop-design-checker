using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class ObjectNavigationRuleTests
{
    public static void Run()
    {
        DeepProjectObjectNavigationIsAttention();
        FrameworkAndValueNavigationIsAllowed();
        DataCarrierNavigationIsAllowed();
        SameObjectAbstractionNavigationIsAllowed();
    }

    private static void DeepProjectObjectNavigationIsAttention()
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

    private static void FrameworkAndValueNavigationIsAllowed()
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

    private static void DataCarrierNavigationIsAllowed()
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

    private static void SameObjectAbstractionNavigationIsAllowed()
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
        var diagnostics = Analyze(source);
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
        var diagnostics = Analyze(source);
        if (diagnostics.Length != 0)
        {
            throw new InvalidOperationException(
                $"Expected no OOP404 diagnostics, found {diagnostics.Length}."
            );
        }
    }

    private static DesignDiagnostic[] Analyze(string source) =>
        new ExcessiveObjectNavigationRule()
            .Analyze(TestProjectFactory.Create(source))
            .Where(diagnostic => diagnostic.Rule.Id == "OOP404")
            .ToArray();
}
