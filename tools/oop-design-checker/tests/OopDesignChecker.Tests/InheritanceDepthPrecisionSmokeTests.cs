using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 継承深度精度スモークテスト
{
    public static void 実行する()
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
        const string ソース = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal class D : C { }
            internal sealed class E : D { }
            """;

        診断が1件であることを確認する(
            new 継承深度過剰規則(),
            テスト用プロジェクト作成器.解析文脈を作る(ソース),
            期待する深さ: 4
        );
    }

    private static void 既定未満の深度を許可する()
    {
        const string ソース = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal sealed class D : C { }
            """;

        診断がないことを確認する(
            new 継承深度過剰規則(),
            テスト用プロジェクト作成器.解析文脈を作る(ソース)
        );
    }

    private static void 設定した深度を適用する()
    {
        const string ソース = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal sealed class D : C { }
            """;

        診断が1件であることを確認する(
            new 継承深度過剰規則(警告深度: 3),
            テスト用プロジェクト作成器.解析文脈を作る(ソース),
            期待する深さ: 3
        );
    }

    private static void CheckerServiceで設定深度を適用する()
    {
        一時プロジェクトを使って実行する(ルートパス =>
        {
            File.WriteAllText(
                Path.Combine(ルートパス, "Sample.cs"),
                """
                internal class A { }
                internal class B : A { }
                internal class C : B { }
                internal sealed class D : C { }
                """
            );
            File.WriteAllText(
                Path.Combine(ルートパス, "oop-design-checker.json"),
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

            var 結果 = CheckerService.Analyze(ルートパス);
            var 診断一覧 = 結果.Diagnostics.Where(項目 => 項目.Rule.Id == "OOP304").ToArray();
            if (
                診断一覧.Length == 1
                && 診断一覧[0]
                    .Message.Contains("configured attention threshold: 3", StringComparison.Ordinal)
            )
            {
                return;
            }

            throw new InvalidOperationException(
                $"Expected configured OOP304 threshold 3 to produce one diagnostic, found {診断一覧.Length}."
            );
        });
    }

    private static void 不正な深度設定を拒否する()
    {
        一時プロジェクトを使って実行する(ルートパス =>
        {
            File.WriteAllText(
                Path.Combine(ルートパス, "Sample.cs"),
                "internal sealed class Sample { }"
            );
            File.WriteAllText(
                Path.Combine(ルートパス, "oop-design-checker.json"),
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
                _ = CheckerService.Analyze(ルートパス);
            }
            catch (InvalidOperationException 例外)
                when (例外.Message.Contains(
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
        const string ソース = """
            internal class A : System.Exception { }
            internal class B : A { }
            internal class C : B { }
            internal class D : C { }
            """;

        診断がないことを確認する(
            new 継承深度過剰規則(),
            テスト用プロジェクト作成器.解析文脈を作る(ソース)
        );
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
        var 文脈 = new 解析文脈(派生プロジェクト, [基底プロジェクト, 派生プロジェクト]);

        診断が1件であることを確認する(new 継承深度過剰規則(), 文脈, 期待する深さ: 4);
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
        var 文脈 = new 解析文脈(派生プロジェクト, [基底プロジェクト, 派生プロジェクト]);

        診断がないことを確認する(new 継承深度過剰規則(), 文脈);
    }

    private static void 部分型宣言で主宣言に診断を1件出す()
    {
        var プロジェクト = ソース群から解析対象プロジェクトを作る(
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
            new 解析文脈(プロジェクト),
            期待する深さ: 4,
            期待するファイル名: "Partial.Primary.cs"
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
        var 文脈 = new 解析文脈(派生プロジェクト, [基底プロジェクト, 派生プロジェクト]);

        診断が1件であることを確認する(
            new 継承深度過剰規則(),
            文脈,
            期待する深さ: 4,
            期待するファイル名: "Derived.Primary.cs"
        );
    }

    private static 解析対象プロジェクト 解析対象プロジェクトを作る(
        string アセンブリ名,
        string ソース,
        params MetadataReference[] 追加参照一覧
    ) =>
        ソース群から解析対象プロジェクトを作る(
            アセンブリ名,
            [($"{アセンブリ名}.cs", ソース)],
            追加参照一覧
        );

    private static 解析対象プロジェクト ソース群から解析対象プロジェクトを作る(
        string アセンブリ名,
        IReadOnlyList<(string FilePath, string Source)> ソース一覧,
        params MetadataReference[] 追加参照一覧
    )
    {
        var 構文木一覧 = ソース一覧
            .Select(ソース => CSharpSyntaxTree.ParseText(ソース.Source, path: ソース.FilePath))
            .ToArray();
        var 参照一覧 = メタデータ参照提供器
            .プラットフォーム参照群を作る()
            .Concat(追加参照一覧)
            .ToArray();
        var コンパイル結果 = CSharpCompilation.Create(
            アセンブリ名,
            構文木一覧,
            参照一覧,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var エラー一覧 = コンパイル結果
            .GetDiagnostics()
            .Where(診断 => 診断.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (エラー一覧.Length > 0)
        {
            throw new InvalidOperationException(
                "Inheritance-depth test project did not compile:\n"
                    + string.Join("\n", エラー一覧.Select(エラー => エラー.ToString()))
            );
        }

        var 意味モデル一覧 = 構文木一覧.ToDictionary(
            構文木 => 構文木,
            構文木 => コンパイル結果.GetSemanticModel(構文木)
        );
        return new 解析対象プロジェクト(
            ルートパス: アセンブリ名,
            アプリケーションである: false,
            コンパイル結果,
            意味モデル一覧
        );
    }

    private static void 一時プロジェクトを使って実行する(Action<string> 処理)
    {
        var ルートパス = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(ルートパス);

        try
        {
            処理(ルートパス);
        }
        finally
        {
            if (Directory.Exists(ルートパス))
            {
                Directory.Delete(ルートパス, recursive: true);
            }
        }
    }

    private static void 診断が1件であることを確認する(
        継承深度過剰規則 規則,
        解析文脈 文脈,
        int 期待する深さ,
        string? 期待するファイル名 = null
    )
    {
        var 診断一覧 = 規則.解析する(文脈).ToArray();
        if (
            診断一覧.Length == 1
            && 診断一覧[0].Rule.Id == "OOP304"
            && 診断一覧[0].Severity == DesignDiagnosticSeverity.Attention
            && 診断一覧[0]
                .Message.Contains(
                    $"Project-owned inheritance depth is {期待する深さ}",
                    StringComparison.Ordinal
                )
            && (
                期待するファイル名 is null
                || Path.GetFileName(診断一覧[0].Location.FilePath) == 期待するファイル名
            )
        )
        {
            return;
        }

        var 検出内容 = string.Join(
            " | ",
            診断一覧.Select(診断 =>
                $"{診断.Rule.Id}:{診断.Severity}:{診断.Location.FilePath}:{診断.Message}"
            )
        );
        throw new InvalidOperationException(
            $"Expected one OOP304 Attention at depth {期待する深さ}"
                + (期待するファイル名 is null ? string.Empty : $" in {期待するファイル名}")
                + $", found [{検出内容}]."
        );
    }

    private static void 診断がないことを確認する(継承深度過剰規則 規則, 解析文脈 文脈)
    {
        var 診断一覧 = 規則.解析する(文脈).Where(項目 => 項目.Rule.Id == "OOP304").ToArray();
        if (診断一覧.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected no OOP304 diagnostic, found {診断一覧.Length}."
        );
    }
}
