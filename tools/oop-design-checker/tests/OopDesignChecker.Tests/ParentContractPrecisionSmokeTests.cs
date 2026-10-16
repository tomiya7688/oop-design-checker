using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 親契約精度スモークテスト
{
    public static void 実行する()
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
        const string ソース = """
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
            ソース,
            "OOP302",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 任意の空実装フックを許可する()
    {
        const string ソース = """
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

        診断がないことを確認する(new 親契約未使用規則(), ソース, "OOP302");
    }

    private static void 抽象親操作の拒否を危険とする()
    {
        const string ソース = """
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
            ソース,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 実装済み親操作の拒否を危険とする()
    {
        const string ソース = """
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
            ソース,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 既に拒否される親操作を許可する()
    {
        const string ソース = """
            internal class Base
            {
                public virtual void Run() => throw new System.NotSupportedException();
            }

            internal sealed class Child : Base
            {
                public override void Run() => throw new System.NotSupportedException();
            }
            """;

        診断がないことを確認する(new 子型による親動作無効化規則(), ソース, "OOP303");
    }

    private static void 外部抽象操作の拒否を危険とする()
    {
        const string ソース = """
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
            ソース,
            "OOP303",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 診断が1件であることを確認する(
        I解析規則 規則,
        string ソース,
        string 規則ID,
        DesignDiagnosticSeverity 重要度
    )
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length == 1 && 診断一覧[0].Rule.Id == 規則ID && 診断一覧[0].Severity == 重要度)
        {
            return;
        }

        var 検出内容 = string.Join(
            ", ",
            診断一覧.Select(項目 => $"{項目.Rule.Id}:{項目.Severity}")
        );
        throw new InvalidOperationException(
            $"Expected one {規則ID}:{重要度} diagnostic, found [{検出内容}]."
        );
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
