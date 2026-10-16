using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Tests;

internal static class テスト用プロジェクト作成器
{
    public static 解析文脈 解析文脈を作る(string ソース)
    {
        var 構文木 = CSharpSyntaxTree.ParseText(ソース, path: "Test.cs");
        var コンパイル結果 = CSharpCompilation.Create(
            "RuleTests",
            [構文木],
            メタデータ参照提供器.プラットフォーム参照群を作る(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var エラー一覧 = コンパイル結果
            .GetDiagnostics()
            .Where(診断 => 診断.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (エラー一覧.Length > 0)
        {
            throw new InvalidOperationException(
                "Test source did not compile:\n"
                    + string.Join("\n", エラー一覧.Select(エラー => エラー.ToString()))
            );
        }

        var 意味モデル一覧 = new Dictionary<SyntaxTree, SemanticModel>
        {
            [構文木] = コンパイル結果.GetSemanticModel(構文木),
        };

        var プロジェクト = new 解析対象プロジェクト(
            ルートパス: ".",
            アプリケーションである: true,
            コンパイル結果,
            意味モデル一覧
        );

        return new 解析文脈(プロジェクト);
    }
}
