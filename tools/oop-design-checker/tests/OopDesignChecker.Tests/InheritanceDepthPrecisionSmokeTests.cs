using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class InheritanceDepthPrecisionSmokeTests
{
    public static void Run()
    {
        既定の深度4を注意とする();
        既定未満の深度を許可する();
        設定した深度を適用する();
        CheckerServiceで設定深度を適用する();
        不正な深度設定を拒否する();
        外部継承を数えない();
        プロジェクトをまたぐ継承を数える();
        外部基底型で継承計測を止める();
        部分型宣言で主宣言に診断を1件出す();
        プロジェクト外の基底型で部分型の計測を止める();
    }

    private static void 既定の深度4を注意とする()
    {
        const string source = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal class D : C { }
            internal sealed class E : D { }
            """;

        診断が1件であることを確認する(
            new 継承深度過剰規則(),
            TestProjectFactory.解析文脈を作る(source),
            expectedDepth: 4
        );
    }

    private static void 既定未満の深度を許可する()
    {
        const string source = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal sealed class D : C { }
            """;

        診断がないことを確認する(new 継承深度過剰規則(), TestProjectFactory.解析文脈を作る(source));
    }

    private static void 設定した深度を適用する()
    {
        const string source = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal sealed class D : C { }
            """;

        診断が1件であることを確認する(
            new 継承深度過剰規則(警告深度: 3),
            TestProjectFactory.解析文脈を作る(source),
            expectedDepth: 3
        );
    }

    private static void CheckerServiceで設定深度を適用する()
    {
        一時プロジェクトを使って実行する(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Sample.cs"),
                """
                internal class A { }
                internal class B : A { }
                internal class C : B { }
                internal sealed class D : C { }
                """
            );
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "ruleSettings": {
                    "oop304": {
                      "warningDepth": 3
                    }
                  }
                }
                """
            );

            var result = CheckerService.Analyze(rootPath);
            var diagnostics = result.Diagnostics.Where(item => item.Rule.Id == "OOP304").ToArray();
            if (
                diagnostics.Length == 1
                && diagnostics[0]
                    .Message.Contains("configured attention threshold: 3", StringComparison.Ordinal)
            )
            {
                return;
            }

            throw new InvalidOperationException(
                $"Expected configured OOP304 threshold 3 to produce one diagnostic, found {diagnostics.Length}."
            );
        });
    }

    private static void 不正な深度設定を拒否する()
    {
        一時プロジェクトを使って実行する(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Sample.cs"),
                "internal sealed class Sample { }"
            );
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "ruleSettings": {
                    "oop304": {
                      "warningDepth": 0
                    }
                  }
                }
                """
            );

            try
            {
                _ = CheckerService.Analyze(rootPath);
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(
                        "ruleSettings.oop304.warningDepth must be at least 1",
                        StringComparison.Ordinal
                    )
                )
            {
                return;
            }

            throw new InvalidOperationException(
                "Invalid OOP304 warningDepth was not rejected with a clear configuration error."
            );
        });
    }

    private static void 外部継承を数えない()
    {
        const string source = """
            internal class A : System.Exception { }
            internal class B : A { }
            internal class C : B { }
            internal class D : C { }
            """;

        診断がないことを確認する(new 継承深度過剰規則(), TestProjectFactory.解析文脈を作る(source));
    }

    private static void プロジェクトをまたぐ継承を数える()
    {
        var 基底プロジェクト = 解析対象プロジェクトを作る(
            "BaseProject",
            """
            public class A { }
            public class B : A { }
            """
        );
        var 派生プロジェクト = 解析対象プロジェクトを作る(
            "DerivedProject",
            """
            public class C : B { }
            public class D : C { }
            public sealed class E : D { }
            """,
            基底プロジェクト.コンパイル結果.ToMetadataReference()
        );
        var context = new 解析文脈(派生プロジェクト, [基底プロジェクト, 派生プロジェクト]);

        診断が1件であることを確認する(new 継承深度過剰規則(), context, expectedDepth: 4);
    }

    private static void 外部基底型で継承計測を止める()
    {
        var 基底プロジェクト = 解析対象プロジェクトを作る(
            "BaseProject",
            """
            public class A : System.Exception { }
            public class B : A { }
            """
        );
        var 派生プロジェクト = 解析対象プロジェクトを作る(
            "DerivedProject",
            """
            public class C : B { }
            public sealed class D : C { }
            """,
            基底プロジェクト.コンパイル結果.ToMetadataReference()
        );
        var context = new 解析文脈(派生プロジェクト, [基底プロジェクト, 派生プロジェクト]);

        診断がないことを確認する(new 継承深度過剰規則(), context);
    }

    private static void 部分型宣言で主宣言に診断を1件出す()
    {
        var project = ソース群から解析対象プロジェクトを作る(
            "PartialProject",
            [
                (
                    "Hierarchy.cs",
                    """
                    internal class A { }
                    internal class B : A { }
                    internal class C : B { }
                    internal class D : C { }
                    """
                ),
                (
                    "Partial.Primary.cs",
                    """
                    internal sealed partial class E : D { }
                    """
                ),
                (
                    "Partial.Secondary.cs",
                    """
                    internal sealed partial class E
                    {
                        public void Run() { }
                    }
                    """
                ),
            ]
        );

        診断が1件であることを確認する(
            new 継承深度過剰規則(),
            new 解析文脈(project),
            expectedDepth: 4,
            expectedFileName: "Partial.Primary.cs"
        );
    }

    private static void プロジェクト外の基底型で部分型の計測を止める()
    {
        var 基底プロジェクト = 解析対象プロジェクトを作る(
            "ExternalBoundaryBaseProject",
            """
            public class A : System.Exception { }
            public class B : A { }
            """
        );
        var 派生プロジェクト = ソース群から解析対象プロジェクトを作る(
            "ExternalBoundaryDerivedProject",
            [
                (
                    "DerivedHierarchy.cs",
                    """
                    public class C : B { }
                    public class D : C { }
                    """
                ),
                (
                    "Derived.Primary.cs",
                    """
                    public sealed partial class E : D { }
                    """
                ),
                (
                    "Derived.Secondary.cs",
                    """
                    public sealed partial class E
                    {
                        public void Run() { }
                    }
                    """
                ),
            ],
            基底プロジェクト.コンパイル結果.ToMetadataReference()
        );
        var context = new 解析文脈(派生プロジェクト, [基底プロジェクト, 派生プロジェクト]);

        診断が1件であることを確認する(
            new 継承深度過剰規則(),
            context,
            expectedDepth: 4,
            expectedFileName: "Derived.Primary.cs"
        );
    }

    private static 解析対象プロジェクト 解析対象プロジェクトを作る(
        string assemblyName,
        string source,
        params MetadataReference[] additionalReferences
    ) =>
        ソース群から解析対象プロジェクトを作る(
            assemblyName,
            [($"{assemblyName}.cs", source)],
            additionalReferences
        );

    private static 解析対象プロジェクト ソース群から解析対象プロジェクトを作る(
        string assemblyName,
        IReadOnlyList<(string FilePath, string Source)> sources,
        params MetadataReference[] additionalReferences
    )
    {
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Source, path: source.FilePath))
            .ToArray();
        var references = メタデータ参照提供器
            .プラットフォーム参照群を作る()
            .Concat(additionalReferences)
            .ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var errors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                "Inheritance-depth test project did not compile:\n"
                    + string.Join("\n", errors.Select(error => error.ToString()))
            );
        }

        var semanticModels = syntaxTrees.ToDictionary(
            syntaxTree => syntaxTree,
            syntaxTree => compilation.GetSemanticModel(syntaxTree)
        );
        return new 解析対象プロジェクト(
            ルートパス: assemblyName,
            アプリケーションである: false,
            compilation,
            semanticModels
        );
    }

    private static void 一時プロジェクトを使って実行する(Action<string> action)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(rootPath);

        try
        {
            action(rootPath);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static void 診断が1件であることを確認する(
        継承深度過剰規則 rule,
        解析文脈 context,
        int expectedDepth,
        string? expectedFileName = null
    )
    {
        var diagnostics = rule.解析する(context).ToArray();
        if (
            diagnostics.Length == 1
            && diagnostics[0].Rule.Id == "OOP304"
            && diagnostics[0].Severity == DesignDiagnosticSeverity.Attention
            && diagnostics[0]
                .Message.Contains(
                    $"Project-owned inheritance depth is {expectedDepth}",
                    StringComparison.Ordinal
                )
            && (
                expectedFileName is null
                || Path.GetFileName(diagnostics[0].Location.FilePath) == expectedFileName
            )
        )
        {
            return;
        }

        var found = string.Join(
            " | ",
            diagnostics.Select(diagnostic =>
                $"{diagnostic.Rule.Id}:{diagnostic.Severity}:{diagnostic.Location.FilePath}:{diagnostic.Message}"
            )
        );
        throw new InvalidOperationException(
            $"Expected one OOP304 Attention at depth {expectedDepth}"
                + (expectedFileName is null ? string.Empty : $" in {expectedFileName}")
                + $", found [{found}]."
        );
    }

    private static void 診断がないことを確認する(継承深度過剰規則 rule, 解析文脈 context)
    {
        var diagnostics = rule.解析する(context).Where(item => item.Rule.Id == "OOP304").ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected no OOP304 diagnostic, found {diagnostics.Length}."
        );
    }
}
