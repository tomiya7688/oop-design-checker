using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class Program
{
    private static readonly IReadOnlyList<TestCase> TestCases =
    [
        new("OOP101 detects public classes confined to an inheritance hierarchy", ExcessiveVisibilityIsDetected),
        new("OOP106 detects public mutable fields as danger", EncapsulationLeakIsDetected),
        new("OOP106 detects mutable collection exposure as danger", MutableCollectionExposureIsDetected),
        new("OOP106 detects unnecessarily public setters as warning", UnnecessaryPublicSetterIsDetected),
        new("OOP106 keeps externally used setters public", ExternallyUsedSetterIsAllowed),
        new("OOP103 static member candidates are attention", StaticMemberCandidateIsDetected),
        new("OOP104 static class candidates are attention", StaticClassCandidateIsDetected),
        new("OOP002 type branching is warning", TypeBranchingIsDetected),
        new("OOP303 disabled parent behavior is danger", DisabledParentBehaviorIsDetected),
        new("OOP304 deep inheritance is attention", DeepInheritanceIsDetected),
        new("core OOP rule sprint", CoreRuleSmokeTests.Run),
        new("configuration disables selected rules", ConfigurationBehaviorTests.DisabledRulesAreSuppressed),
        new("configuration ignores matching paths", ConfigurationBehaviorTests.IgnoredPathsAreExcluded),
        new("configuration controls failure threshold", ConfigurationBehaviorTests.FailureThresholdIsLoaded)
    ];

    private static int Main()
    {
        var failed = 0;

        foreach (var testCase in TestCases)
        {
            try
            {
                testCase.Run();
                Console.WriteLine($"PASS {testCase.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {testCase.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"{TestCases.Count - failed}/{TestCases.Count} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void ExcessiveVisibilityIsDetected()
    {
        const string source = """
            public class Base
            {
            }

            internal sealed class Child : Base
            {
            }
            """;

        var diagnostics = Run(new ExcessiveVisibilityRule(), source);
        AssertSingleRule(diagnostics, "OOP101", DesignDiagnosticSeverity.Warning);
    }

    private static void EncapsulationLeakIsDetected()
    {
        const string source = """
            internal sealed class Sample
            {
                public int Value;
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertSingleRule(diagnostics, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void MutableCollectionExposureIsDetected()
    {
        const string source = """
            using System.Collections.Generic;

            internal sealed class Bag
            {
                private readonly List<int> _items = new();

                public List<int> Items => _items;
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertSingleRule(diagnostics, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void UnnecessaryPublicSetterIsDetected()
    {
        const string source = """
            internal sealed class Counter
            {
                public int Value { get; set; }

                public void Increment()
                {
                    Value++;
                }
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertSingleRule(diagnostics, "OOP106", DesignDiagnosticSeverity.Warning);
    }

    private static void ExternallyUsedSetterIsAllowed()
    {
        const string source = """
            internal sealed class Counter
            {
                public int Value { get; set; }

                public void Increment()
                {
                    Value++;
                }
            }

            internal sealed class CounterEditor
            {
                public void Reset(Counter counter)
                {
                    counter.Value = 0;
                }
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertRuleCount(diagnostics, "OOP106", 0);
    }

    private static void StaticMemberCandidateIsDetected()
    {
        const string source = """
            internal sealed class Calculator
            {
                private int _offset;

                public int Add(int left, int right) => left + right;
                public int AddOffset(int value) => value + _offset;
            }
            """;

        var diagnostics = Run(new StaticMemberCandidateRule(), source);
        AssertSingleRule(diagnostics, "OOP103", DesignDiagnosticSeverity.Attention);
    }

    private static void StaticClassCandidateIsDetected()
    {
        const string source = """
            internal class Utility
            {
                public int Add(int left, int right) => left + right;
            }
            """;

        var diagnostics = Run(new StaticClassCandidateRule(), source);
        AssertSingleRule(diagnostics, "OOP104", DesignDiagnosticSeverity.Attention);
    }

    private static void TypeBranchingIsDetected()
    {
        const string source = """
            internal abstract class Animal
            {
            }

            internal sealed class Dog : Animal
            {
            }

            internal sealed class Cat : Animal
            {
            }

            internal sealed class Handler
            {
                public void Handle(Animal animal)
                {
                    if (animal is Dog)
                    {
                    }
                    else if (animal is Cat)
                    {
                    }
                }
            }
            """;

        var diagnostics = Run(new TypeBranchPolymorphismRule(), source);
        AssertSingleRule(diagnostics, "OOP002", DesignDiagnosticSeverity.Warning);
    }

    private static void DisabledParentBehaviorIsDetected()
    {
        const string source = """
            using System;

            internal abstract class Base
            {
                public abstract void Run();
            }

            internal sealed class Child : Base
            {
                public override void Run() => throw new NotSupportedException();
            }
            """;

        var diagnostics = Run(new ChildDisablesParentBehaviorRule(), source);
        AssertSingleRule(diagnostics, "OOP303", DesignDiagnosticSeverity.Danger);
    }

    private static void DeepInheritanceIsDetected()
    {
        const string source = """
            internal class A
            {
            }

            internal class B : A
            {
            }

            internal class C : B
            {
            }

            internal class D : C
            {
            }

            internal sealed class E : D
            {
            }
            """;

        var diagnostics = Run(new ExcessiveInheritanceDepthRule(), source);
        AssertSingleRule(diagnostics, "OOP304", DesignDiagnosticSeverity.Attention);
    }

    private static IReadOnlyList<DesignDiagnostic> Run(IAnalysisRule rule, string source) =>
        rule.Analyze(TestProjectFactory.Create(source)).ToArray();

    private static void AssertSingleRule(
        IReadOnlyList<DesignDiagnostic> diagnostics,
        string ruleId,
        DesignDiagnosticSeverity severity)
    {
        AssertRuleCount(diagnostics, ruleId, 1);

        if (diagnostics[0].Severity != severity)
        {
            throw new InvalidOperationException(
                $"Expected severity {severity}, but found {diagnostics[0].Severity}.");
        }
    }

    private static void AssertRuleCount(
        IReadOnlyList<DesignDiagnostic> diagnostics,
        string ruleId,
        int expectedCount)
    {
        var matching = diagnostics.Where(diagnostic => diagnostic.Rule.Id == ruleId).ToArray();
        if (matching.Length != expectedCount)
        {
            var found = string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Rule.Id));
            throw new InvalidOperationException(
                $"Expected {expectedCount} {ruleId} diagnostic(s), found {matching.Length}. All diagnostics: [{found}]");
        }
    }

    private sealed record TestCase(string Name, Action Run);
}
