using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class ParentContractPrecisionSmokeTests
{
    public static void Run()
    {
        意味のある親操作を無効化した場合に警告する();
        任意の空実装フックを許可する();
        抽象親操作の拒否を危険とする();
        実装済み親操作の拒否を危険とする();
        既に拒否される親操作を許可する();
        外部抽象操作の拒否を危険とする();
    }

    private static void 意味のある親操作を無効化した場合に警告する()
    {
        const string source = """
            internal class Base
            {
                private int _started;

                public virtual void Start() => _started++;
                public virtual object? Read() => new object();
            }

            internal sealed class Child : Base
            {
                public override void Start() { }
                public override object? Read() => null;
            }
            """;

        診断が1件であることを確認する(
            new 親契約未使用規則(),
            source,
            "OOP302",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 任意の空実装フックを許可する()
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

        診断がないことを確認する(new 親契約未使用規則(), source, "OOP302");
    }

    private static void 抽象親操作の拒否を危険とする()
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

        診断が1件であることを確認する(
            new 子型による親動作無効化規則(),
            source,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 実装済み親操作の拒否を危険とする()
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

        診断が1件であることを確認する(
            new 子型による親動作無効化規則(),
            source,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 既に拒否される親操作を許可する()
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

        診断がないことを確認する(new 子型による親動作無効化規則(), source, "OOP303");
    }

    private static void 外部抽象操作の拒否を危険とする()
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

        診断が1件であることを確認する(
            new 子型による親動作無効化規則(),
            source,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 診断が1件であることを確認する(
        I解析規則 rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
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

    private static void 診断がないことを確認する(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }
}
