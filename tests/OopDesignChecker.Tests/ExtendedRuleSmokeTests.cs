using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class ExtendedRuleSmokeTests
{
    public static void Run()
    {
        UnusedSingleImplementationAbstractionIsAttention();
        InvariantBypassIsDanger();
        ExternalStateManipulationIsWarning();
        SuspiciousHiddenInheritanceIsWarning();
        ParentContractNoOpsAreWarning();
        ImplementationReuseInheritanceIsAttention();
        AnemicObjectWithExternalBehaviorIsWarning();
        SerializableDataCarrierIsAllowed();
        DataContractCarrierIsAllowed();
        NamingAloneDoesNotExemptDomainBehavior();
        MapperProjectionDoesNotMakeObjectAnemic();
        UnrelatedDependencyClustersAreWarning();
        HelperConnectedDependenciesAreAllowed();
        SharedAbstractionDependenciesAreAllowed();
        DeepObjectNavigationIsAttention();
        GetterSetterOnlyObjectIsAttention();
        MessageDataCarrierIsAllowed();
    }

    private static void UnusedSingleImplementationAbstractionIsAttention()
    {
        const string source = """
            internal interface IUnused
            {
                void Run();
            }

            internal sealed class OnlyImplementation : IUnused
            {
                public void Run() { }
            }
            """;

        AssertSingle(
            new UnnecessaryAbstractionRule(),
            source,
            "OOP003",
            DesignDiagnosticSeverity.Attention
        );
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

    private static void SuspiciousHiddenInheritanceIsWarning()
    {
        const string source = """
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

        AssertSingle(
            new SuspiciousInheritanceRelationshipRule(),
            source,
            "OOP301",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ParentContractNoOpsAreWarning()
    {
        const string source = """
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

    private static void AnemicObjectWithExternalBehaviorIsWarning()
    {
        const string source = """
            internal sealed class Position
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
            }

            internal sealed class PositionService
            {
                public int Magnitude(Position position)
                {
                    return position.X + position.Y + position.Z;
                }
            }
            """;

        AssertSingle(new AnemicObjectRule(), source, "OOP402", DesignDiagnosticSeverity.Warning);
    }

    private static void SerializableDataCarrierIsAllowed()
    {
        const string source = """
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

        AssertNone(new AnemicObjectRule(), source, "OOP402");
        AssertNone(new GetterSetterOnlyObjectRule(), source, "OOP405");
    }

    private static void DataContractCarrierIsAllowed()
    {
        const string source = """
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

        AssertNone(new AnemicObjectRule(), source, "OOP402");
        AssertNone(new GetterSetterOnlyObjectRule(), source, "OOP405");
    }

    private static void NamingAloneDoesNotExemptDomainBehavior()
    {
        const string source = """
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

        AssertSingle(
            new GetterSetterOnlyObjectRule(),
            source,
            "OOP405",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void MapperProjectionDoesNotMakeObjectAnemic()
    {
        const string source = """
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

        AssertNone(new AnemicObjectRule(), source, "OOP402");
    }

    private static void UnrelatedDependencyClustersAreWarning()
    {
        const string source = """
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

        AssertSingle(
            new ExcessiveUnrelatedDependenciesRule(),
            source,
            "OOP403",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void HelperConnectedDependenciesAreAllowed()
    {
        const string source = """
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

        AssertNone(new ExcessiveUnrelatedDependenciesRule(), source, "OOP403");
    }

    private static void SharedAbstractionDependenciesAreAllowed()
    {
        const string source = """
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

        AssertNone(new ExcessiveUnrelatedDependenciesRule(), source, "OOP403");
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

    private static void MessageDataCarrierIsAllowed()
    {
        const string source = """
            internal sealed class UserCreatedMessage
            {
                public string UserId { get; set; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
                public long Timestamp { get; set; }
            }
            """;

        AssertNone(new GetterSetterOnlyObjectRule(), source, "OOP405");
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

    private static void AssertNone(IAnalysisRule rule, string source, string ruleId)
    {
        var diagnostics = rule.Analyze(TestProjectFactory.Create(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }
}
