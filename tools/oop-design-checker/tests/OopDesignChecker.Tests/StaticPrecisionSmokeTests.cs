using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class StaticPrecisionSmokeTests
{
    public static void Run()
    {
        OrdinaryExternalTypeAttributeDoesNotSuppressVisibilityDiagnostic();
        ExternalContractIsNotSealingCandidate();
        ExternalBaseContractMethodsAreNotStaticCandidates();
        OrdinaryExternalTypeAttributeDoesNotSuppressStaticClassCandidate();
        OrdinaryExternalMethodAttributeDoesNotSuppressStaticMemberCandidate();
        FrameworkCallbackMethodIsNotStaticCandidate();
        AttributedTypeIsNotStaticClassCandidate();
        SerializableTypeRemainsSealingCandidate();
        MutableStaticPropertyIsWarning();
        ReadonlyMutableCollectionIsWarning();
        ThreadStaticFieldIsAllowed();
    }

    private static void OrdinaryExternalTypeAttributeDoesNotSuppressVisibilityDiagnostic()
    {
        const string source = """
            [System.Obsolete]
            public class Base
            {
            }

            internal class Child : Base
            {
            }
            """;

        AssertSingleRule(new ExcessiveVisibilityRule(), source, "OOP101");
    }

    private static void ExternalContractIsNotSealingCandidate()
    {
        const string source = """
            internal class SampleException : System.Exception
            {
            }
            """;

        AssertNone(new SealingCandidateRule(), source, "OOP102");
    }

    private static void ExternalBaseContractMethodsAreNotStaticCandidates()
    {
        const string source = """
            internal class SampleException : System.Exception
            {
                private int _count;

                public void Touch() => _count++;
                public string Describe() => "sample";
            }
            """;

        AssertNone(new StaticMemberCandidateRule(), source, "OOP103");
    }

    private static void FrameworkCallbackMethodIsNotStaticCandidate()
    {
        const string source = """
            using System.Runtime.Serialization;

            internal sealed class Worker
            {
                private int _count;

                public void Touch() => _count++;

                [OnDeserialized]
                public void Callback(StreamingContext context)
                {
                }
            }
            """;

        AssertNone(new StaticMemberCandidateRule(), source, "OOP103");
    }

    private static void OrdinaryExternalTypeAttributeDoesNotSuppressStaticClassCandidate()
    {
        const string source = """
            [System.Obsolete]
            internal class Formatter
            {
                public int Version() => 1;
            }
            """;

        AssertSingleRule(new StaticClassCandidateRule(), source, "OOP104");
    }

    private static void OrdinaryExternalMethodAttributeDoesNotSuppressStaticMemberCandidate()
    {
        const string source = """
            internal sealed class Worker
            {
                private int _count;

                public void Touch() => _count++;

                [System.Obsolete]
                public int Version() => 1;
            }
            """;

        AssertSingleRule(new StaticMemberCandidateRule(), source, "OOP103");
    }

    private static void AttributedTypeIsNotStaticClassCandidate()
    {
        const string source = """
            [System.Serializable]
            internal class SnapshotFormatter
            {
                public int Version() => 1;
            }
            """;

        AssertNone(new StaticClassCandidateRule(), source, "OOP104");
    }

    private static void SerializableTypeRemainsSealingCandidate()
    {
        const string source = """
            [System.Serializable]
            internal class Snapshot
            {
            }
            """;

        AssertSingleRule(new SealingCandidateRule(), source, "OOP102");
    }

    private static void MutableStaticPropertyIsWarning()
    {
        const string source = """
            internal static class GlobalState
            {
                public static int Count { get; set; }
            }
            """;

        AssertSingle(
            new StatefulStaticDesignRule(),
            source,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ReadonlyMutableCollectionIsWarning()
    {
        const string source = """
            using System.Collections.Generic;

            internal static class GlobalState
            {
                private static readonly List<int> Items = new();

                public static void Add(int value) => Items.Add(value);
            }
            """;

        AssertSingle(
            new StatefulStaticDesignRule(),
            source,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ThreadStaticFieldIsAllowed()
    {
        const string source = """
            internal static class PerThreadState
            {
                [System.ThreadStatic]
                private static int _count;

                public static void Increment() => _count++;
            }
            """;

        AssertNone(new StatefulStaticDesignRule(), source, "OOP105");
    }

    private static void AssertNone(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.Create(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }

    private static void AssertSingleRule(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.Create(source)).ToArray();
        if (diagnostics.Length == 1 && diagnostics[0].Rule.Id == ruleId)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected one {ruleId} diagnostic, found [{found}].");
    }

    private static void AssertSingle(
        StatefulStaticDesignRule rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.解析する(TestProjectFactory.Create(source)).ToArray();
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
