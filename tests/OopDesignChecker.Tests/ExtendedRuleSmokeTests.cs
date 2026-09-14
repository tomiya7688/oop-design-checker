using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class ExtendedRuleSmokeTests
{
    public static void Run()
    {
        InvariantBypassIsDanger();
        ExternalStateManipulationIsWarning();
        ParentContractNoOpsAreWarning();
        ImplementationReuseInheritanceIsAttention();
        DeepObjectNavigationIsAttention();
        GetterSetterOnlyObjectIsAttention();
    }

    private static void InvariantBypassIsDanger()
    {
        const string source = """
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

        AssertSingle(
            new ObjectInvariantBypassRule(),
            source,
            "OOP107",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void ExternalStateManipulationIsWarning()
    {
        const string source = """
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

        AssertSingle(
            new ExcessiveExternalStateManipulationRule(),
            source,
            "OOP108",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ParentContractNoOpsAreWarning()
    {
        const string source = """
            internal class Base
            {
                public virtual void Start() { }
                public virtual object? Read() => new object();
            }

            internal sealed class Child : Base
            {
                public override void Start() { }
                public override object? Read() => null;
            }
            """;

        AssertSingle(
            new ParentContractMostlyUnusedRule(),
            source,
            "OOP302",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ImplementationReuseInheritanceIsAttention()
    {
        const string source = """
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

        AssertSingle(
            new CompositionCandidateRule(),
            source,
            "OOP307",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void DeepObjectNavigationIsAttention()
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

        AssertSingle(
            new ExcessiveObjectNavigationRule(),
            source,
            "OOP404",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void GetterSetterOnlyObjectIsAttention()
    {
        const string source = """
            internal sealed class Person
            {
                public string Name { get; set; } = string.Empty;
                public int Age { get; set; }
                public string City { get; set; } = string.Empty;
                public string Country { get; set; } = string.Empty;
            }
            """;

        AssertSingle(
            new GetterSetterOnlyObjectRule(),
            source,
            "OOP405",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void AssertSingle(
        IAnalysisRule rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.Analyze(TestProjectFactory.Create(source)).ToArray();
        if (
            diagnostics.Length != 1
            || diagnostics[0].Rule.Id != ruleId
            || diagnostics[0].Severity != severity
        )
        {
            var found = string.Join(
                ", ",
                diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
            );
        }
    }
}
