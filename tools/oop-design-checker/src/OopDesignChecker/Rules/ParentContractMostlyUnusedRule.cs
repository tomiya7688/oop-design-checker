using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 親契約未使用規則 : I解析規則
{
    private const int 最小空実装オーバーライド数 = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP302", "Parent contract mostly unused", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 宣言 in 構文根.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol シンボル
                    || !シンボル操作器.主宣言である(シンボル, 宣言)
                )
                {
                    continue;
                }

                var 無効化操作群 = 宣言
                    .Members.OfType<MethodDeclarationSyntax>()
                    .Where(メソッド => 親契約を無効化する(メソッド, 意味モデル))
                    .ToArray();
                if (無効化操作群.Length < 最小空実装オーバーライド数)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"This child type neutralizes {無効化操作群.Length} required or supported inherited operations with no-op/default implementations. The parent contract may be broader than this object can meaningfully support.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }

    private static bool 親契約を無効化する(MethodDeclarationSyntax 宣言, SemanticModel 意味モデル)
    {
        if (
            意味モデル.GetDeclaredSymbol(宣言) is not IMethodSymbol { IsOverride: true } メソッド
            || !継承契約判定器.空実装または既定実装である(宣言, メソッド)
        )
        {
            return false;
        }

        return 継承契約判定器.親操作契約を分類する(メソッド)
            is 親操作契約.必須
                or 親操作契約.実装済み;
    }
}
