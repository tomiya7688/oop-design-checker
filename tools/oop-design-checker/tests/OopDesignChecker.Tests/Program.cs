using Microsoft.CodeAnalysis;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class スモークテスト実行処理
{
    private static readonly IReadOnlyList<テスト項目> テスト項目一覧 =
    [
        new(
            "project loader resolves real project/package references",
            プロジェクト読み込み器がプロジェクト参照を解決する
        ),
        new(
            "project loader rejects broken loose-source compilations",
            プロジェクト読み込み器が不正なコンパイルを拒否する
        ),
        new(
            "project loader keeps multi-project compilations separate",
            プロジェクト読み込み器が複数プロジェクトを別々に保つ
        ),
        new(
            "checker service analyzes SDK-style project folders",
            CheckerServiceがSDK形式のプロジェクトフォルダーを解析する
        ),
        new(
            "project loader analyzes solution root folders",
            プロジェクト読み込み器がソリューションルートを読み込む
        ),
        new(
            "diagnostic identity preserves platform path case semantics",
            診断識別情報がパスの大文字小文字規則を維持する
        ),
        new(
            "OOP101 detects public classes confined to an inheritance hierarchy",
            過剰な可視性を検出する
        ),
        new("OOP106 detects public mutable fields as danger", カプセル化漏出を検出する),
        new(
            "OOP106 detects mutable collection exposure as danger",
            可変コレクションの公開を検出する
        ),
        new("OOP106 detects unnecessarily public setters as warning", 不要な公開設定子を検出する),
        new("OOP106 keeps externally used setters public", 外部使用される設定子を許可する),
        new("OOP103 static member candidates are attention", 静的メンバー候補を検出する),
        new(
            "OOP103 does not suggest static when inherited instance state is used",
            継承したインスタンス状態を静的候補にしない
        ),
        new("OOP104 static class candidates are attention", 静的クラス候補を検出する),
        new("OOP002 type branching is warning", 型による分岐を検出する),
        new("OOP303 disabled parent behavior is danger", 無効化された親の振る舞いを検出する),
        new("OOP304 deep inheritance is attention", 深い継承を検出する),
        new("core OOP rule sprint", 基本規則スモークテスト.実行する),
        new("CI diagnostic output formats", 出力形式スモークテスト.実行する),
        new(
            "shared product version resolution handles metadata and fallbacks",
            製品版情報解決テスト.実行する
        ),
        new("Japanese and English UI localization", 表示言語スモークテスト.実行する),
        new(
            "GUI configuration, export, and cancellation workflow",
            GUIワークフロー第2段階スモークテスト.実行する
        ),
        new("configuration disables selected rules", 設定動作テスト.無効化した規則の診断を抑制する),
        new("configuration ignores matching paths", 設定動作テスト.無視対象のパスを除外する),
        new("configuration controls failure threshold", 設定動作テスト.失敗基準を読み込む),
    ];

    private static int Main()
    {
        var 失敗数 = 0;

        foreach (var テスト項目 in テスト項目一覧)
        {
            try
            {
                テスト項目.実行する();
                Console.WriteLine($"PASS {テスト項目.Name}");
            }
            catch (Exception 例外)
            {
                失敗数++;
                Console.Error.WriteLine($"FAIL {テスト項目.Name}");
                Console.Error.WriteLine(例外.Message);
            }
        }

        Console.WriteLine($"{テスト項目一覧.Count - 失敗数}/{テスト項目一覧.Count} tests passed.");
        return 失敗数 == 0 ? 0 : 1;
    }

    private static void プロジェクト読み込み器がプロジェクト参照を解決する()
    {
        var リポジトリルート = リポジトリルートを取得する();
        var プロジェクトパス = Path.Combine(
            リポジトリルート,
            "src",
            "OopDesignChecker",
            "OopDesignChecker.csproj"
        );

        var プロジェクト = new CSharpプロジェクト読み込み器().読み込む(プロジェクトパス);
        var エラー一覧 = プロジェクト
            .コンパイル結果.GetDiagnostics()
            .Where(診断 => 診断.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (エラー一覧.Length > 0)
        {
            throw new InvalidOperationException(
                "Expected project compilation without errors:\n"
                    + string.Join("\n", エラー一覧.Select(エラー => エラー.ToString()))
            );
        }

        if (プロジェクト.構文木群.Any(ツリー => パス除外判定器.無視する(ツリー.FilePath)))
        {
            throw new InvalidOperationException(
                "Generated bin/obj source leaked into rule traversal."
            );
        }
    }

    private static void プロジェクト読み込み器が複数プロジェクトを別々に保つ()
    {
        var リポジトリルート = リポジトリルートを取得する();
        var 一時ソリューション = Path.Combine(
            リポジトリルート,
            $"oop-checker-multi-project-{Guid.NewGuid():N}.slnx"
        );

        File.WriteAllText(
            一時ソリューション,
            """
            <Solution>
              <Project Path="src/OopDesignChecker/OopDesignChecker.csproj" />
              <Project Path="src/frontends/cui/OopDesignChecker.Cui.csproj" />
            </Solution>
            """
        );

        try
        {
            var プロジェクト一覧 = new CSharpプロジェクト読み込み器().プロジェクト群を読み込む(
                一時ソリューション
            );
            if (プロジェクト一覧.Count != 2)
            {
                throw new InvalidOperationException(
                    $"Expected 2 separately loaded projects, found {プロジェクト一覧.Count}."
                );
            }

            var アセンブリ名一覧 = プロジェクト一覧
                .Select(プロジェクト => プロジェクト.コンパイル結果.AssemblyName)
                .Where(名前 => !string.IsNullOrWhiteSpace(名前))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (アセンブリ名一覧.Length != 2)
            {
                throw new InvalidOperationException(
                    "Expected each project to retain its own compilation."
                );
            }
        }
        finally
        {
            File.Delete(一時ソリューション);
        }
    }

    private static void CheckerServiceがSDK形式のプロジェクトフォルダーを解析する()
    {
        var 一時ディレクトリ = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-folder-project-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(一時ディレクトリ);

        try
        {
            File.WriteAllText(
                Path.Combine(一時ディレクトリ, "FolderTarget.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """
            );
            File.WriteAllText(
                Path.Combine(一時ディレクトリ, "Sample.cs"),
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            _ = CheckerService.Analyze(一時ディレクトリ);
        }
        finally
        {
            Directory.Delete(一時ディレクトリ, recursive: true);
        }
    }

    private static void プロジェクト読み込み器がソリューションルートを読み込む()
    {
        var リポジトリルート = リポジトリルートを取得する();
        var プロジェクト一覧 = new CSharpプロジェクト読み込み器().プロジェクト群を読み込む(
            リポジトリルート
        );
        if (プロジェクト一覧.Count < 1)
        {
            throw new InvalidOperationException(
                "Expected the solution root folder to load at least one C# project."
            );
        }
    }

    private static void 診断識別情報がパスの大文字小文字規則を維持する()
    {
        var ルート = Path.Combine(Path.GetTempPath(), $"oop-checker-case-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ルート);

        try
        {
            var 大文字 = パスに対応するプロジェクトを作る(Path.Combine(ルート, "Foo.cs"));
            var 小文字 = パスに対応するプロジェクトを作る(Path.Combine(ルート, "foo.cs"));
            var 診断一覧 = new 解析実行器([new カプセル化漏出規則()])
                .解析する([大文字, 小文字])
                .Where(診断 => 診断.Rule.Id == "OOP106")
                .ToArray();

            var 期待値 = OperatingSystem.IsWindows() ? 1 : 2;
            if (診断一覧.Length != 期待値)
            {
                throw new InvalidOperationException(
                    $"Expected {期待値} case-distinct diagnostic(s), found {診断一覧.Length}."
                );
            }
        }
        finally
        {
            Directory.Delete(ルート, recursive: true);
        }
    }

    private static 解析対象プロジェクト パスに対応するプロジェクトを作る(string ファイルパス)
    {
        const string ソース = "internal sealed class Same { public int Value; }";
        var 構文木 = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            ソース,
            path: ファイルパス
        );
        var コンパイル結果 = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(ファイルパス),
            [構文木],
            メタデータ参照提供器.プラットフォーム参照群を作る(),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
            )
        );
        var 意味モデル一覧 = new Dictionary<
            Microsoft.CodeAnalysis.SyntaxTree,
            Microsoft.CodeAnalysis.SemanticModel
        >
        {
            [構文木] = コンパイル結果.GetSemanticModel(構文木),
        };

        return new 解析対象プロジェクト(
            Path.GetDirectoryName(ファイルパス)!,
            アプリケーションである: false,
            コンパイル結果,
            意味モデル一覧,
            [構文木]
        );
    }

    private static void プロジェクト読み込み器が不正なコンパイルを拒否する()
    {
        var 一時ディレクトリ = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-test-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(一時ディレクトリ);

        try
        {
            var ソースパス = Path.Combine(一時ディレクトリ, "Broken.cs");
            File.WriteAllText(ソースパス, "internal sealed class Broken { MissingType Value; }");

            try
            {
                _ = new CSharpプロジェクト読み込み器().読み込む(ソースパス);
            }
            catch (InvalidOperationException 例外)
                when (例外.Message.Contains(
                        "could not be analyzed reliably",
                        StringComparison.Ordinal
                    )
                )
            {
                return;
            }

            throw new InvalidOperationException(
                "Broken source was accepted as a reliable compilation."
            );
        }
        finally
        {
            Directory.Delete(一時ディレクトリ, recursive: true);
        }
    }

    private static void 過剰な可視性を検出する()
    {
        const string ソース = """
            public class Base
            {
            }

            internal sealed class Child : Base
            {
            }
            """;

        var 診断一覧 = 実行する(new 過剰可視性規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP101", DesignDiagnosticSeverity.Warning);
    }

    private static void カプセル化漏出を検出する()
    {
        const string ソース = """
            internal sealed class Sample
            {
                public int Value;
            }
            """;

        var 診断一覧 = 実行する(new カプセル化漏出規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void 可変コレクションの公開を検出する()
    {
        const string ソース = """
            using System.Collections.Generic;

            internal sealed class Bag
            {
                private readonly List<int> _items = new();

                public List<int> Items => _items;
            }
            """;

        var 診断一覧 = 実行する(new カプセル化漏出規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void 不要な公開設定子を検出する()
    {
        const string ソース = """
            internal sealed class Counter
            {
                public int Value { get; set; }

                public void Increment()
                {
                    Value++;
                }
            }
            """;

        var 診断一覧 = 実行する(new カプセル化漏出規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP106", DesignDiagnosticSeverity.Warning);
    }

    private static void 外部使用される設定子を許可する()
    {
        const string ソース = """
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

        var 診断一覧 = 実行する(new カプセル化漏出規則(), ソース);
        規則数を確認する(診断一覧, "OOP106", 0);
    }

    private static void 静的メンバー候補を検出する()
    {
        const string ソース = """
            internal sealed class Calculator
            {
                private int _offset;

                public int Add(int left, int right) => left + right;
                public int AddOffset(int value) => value + _offset;
            }
            """;

        var 診断一覧 = 実行する(new 静的メンバー候補規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP103", DesignDiagnosticSeverity.Attention);
    }

    private static void 継承したインスタンス状態を静的候補にしない()
    {
        const string ソース = """
            internal class BaseCalculator
            {
                protected int Offset => 1;
            }

            internal sealed class Calculator : BaseCalculator
            {
                public int AddOffset(int value) => value + Offset;
            }
            """;

        var 診断一覧 = 実行する(new 静的メンバー候補規則(), ソース);
        規則数を確認する(診断一覧, "OOP103", 0);
    }

    private static void 静的クラス候補を検出する()
    {
        const string ソース = """
            internal class Utility
            {
                public int Add(int left, int right) => left + right;
            }
            """;

        var 診断一覧 = 実行する(new 静的クラス候補規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP104", DesignDiagnosticSeverity.Attention);
    }

    private static void 型による分岐を検出する()
    {
        const string ソース = """
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

        var 診断一覧 = 実行する(new 型分岐ポリモーフィズム規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP002", DesignDiagnosticSeverity.Warning);
    }

    private static void 無効化された親の振る舞いを検出する()
    {
        const string ソース = """
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

        var 診断一覧 = 実行する(new 子型による親動作無効化規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP303", DesignDiagnosticSeverity.Danger);
    }

    private static void 深い継承を検出する()
    {
        const string ソース = """
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

        var 診断一覧 = 実行する(new 継承深度過剰規則(), ソース);
        単一規則の診断を確認する(診断一覧, "OOP304", DesignDiagnosticSeverity.Attention);
    }

    private static DesignDiagnostic[] 実行する(I解析規則 規則, string ソース) =>
        規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();

    private static string リポジトリルートを取得する()
    {
        var 候補一覧 = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };

        foreach (var 候補 in 候補一覧)
        {
            var 現在値 = new DirectoryInfo(候補);
            while (現在値 is not null)
            {
                if (File.Exists(Path.Combine(現在値.FullName, "OopDesignChecker.sln")))
                {
                    return 現在値.FullName;
                }

                現在値 = 現在値.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate repository root for integration test."
        );
    }

    private static void 単一規則の診断を確認する(
        DesignDiagnostic[] 診断一覧,
        string 規則ID,
        DesignDiagnosticSeverity 重要度
    )
    {
        規則数を確認する(診断一覧, 規則ID, 1);

        if (診断一覧[0].Severity != 重要度)
        {
            throw new InvalidOperationException(
                $"Expected severity {重要度}, but found {診断一覧[0].Severity}."
            );
        }
    }

    private static void 規則数を確認する(
        DesignDiagnostic[] 診断一覧,
        string 規則ID,
        int 期待する件数
    )
    {
        var 一致項目 = 診断一覧.Where(診断 => 診断.Rule.Id == 規則ID).ToArray();
        if (一致項目.Length != 期待する件数)
        {
            var 検出内容 = string.Join(", ", 診断一覧.Select(診断 => 診断.Rule.Id));
            throw new InvalidOperationException(
                $"Expected {期待する件数} {規則ID} diagnostic(s), found {一致項目.Length}. All diagnostics: [{検出内容}]"
            );
        }
    }

    private sealed record テスト項目(string Name, Action 実行する);
}
