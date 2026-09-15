using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class ParentContractPrecisionSmokeTests
{
    public static void Run()
    {
        MeaningfulParentOperationsNeutralizedAreWarning();
        OptionalNoOpHooksAreAllowed();
        AbstractParentOperationRejectedIsDanger();
        SupportedParentOperationRejectedIsDanger();
        AlreadyRejectedParentOperationIsAllowed();
        ExternalAbstractOperationRejectedIsDanger();
    }

    private static void MeaningfulParentOperationsNeutralizedAreWarning()
    {
        const string source = """
            internal class Base
            {
                private int _started;

                public virtual void Start() => _started++;
                public virtual object Read() => new object();
            }

            internal sealed class Child : Base
            {
                public override void Start() { }
                public override object Read() => null!;
            }
            """;

        AssertSingle(
            new ParentContractMostlyUnusedRule(),
            source,
            "OOP302",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void OptionalNoOpHooksAreAllowed()
    {
        const string source = """
            internal class Template
            {
                protected virtual void BeforeRun() { }
                protected virtual object? ReadOptional() => null;
            }

            internal sealed class SpecializedTemplate : Template
            {
                protected override void BeforeRun() { }
                protected override object? ReadOptional() => null;
            }
            """;

        AssertNone(new ParentContractMostlyUnusedRule(), source, "OOP302");
    }

    private static void AbstractParentOperationRejectedIsDanger()
    {
        const string source = """
            internal abstract class Base
            {
                public abstract void Run();
            }

            internal sealed class Child : Base
            {
                public override void Run() => throw new System.NotSupportedException();
            }
            """;

        AssertSingle(
            new ChildDisablesParentBehaviorRule(),
            source,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void SupportedParentOperationRejectedIsDanger()
    {
        const string source = """
            internal class Base
            {
                private int _runs;
                public virtual void Run() => _runs++;
            }

            internal sealed class Child : Base
            {
                public override void Run() => throw new System.NotImplementedException();
            }
            """;

        AssertSingle(
            new ChildDisablesParentBehaviorRule(),
            source,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void AlreadyRejectedParentOperationIsAllowed()
    {
        const string source = """
            internal class Base
            {
                public virtual void Run() => throw new System.NotSupportedException();
            }

            internal sealed class Child : Base
            {
                public override void Run() => throw new System.NotSupportedException();
            }
            """;

        AssertNone(new ChildDisablesParentBehaviorRule(), source, "OOP303");
    }

    private static void ExternalAbstractOperationRejectedIsDanger()
    {
        const string source = """
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            internal sealed class RejectingHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(
                    HttpRequestMessage request,
                    CancellationToken cancellationToken
                ) => throw new NotSupportedException();
            }
            """;

        AssertSingle(
            new ChildDisablesParentBehaviorRule(),
            source,
            "OOP303",
            DesignDiagnosticSeverity.Danger
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
            diagnostics.Length == 1
            && diagnostics[0].Rule.Id == ruleId
            && diagnostics[0].Severity == severity
        )
        {
            return;
        }

        var found = string.Join(
            ", ",
            diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
        );
        throw new InvalidOperationException(
            $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
        );
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
