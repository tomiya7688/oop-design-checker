using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 静的クラス候補規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP104", "Static class candidate", DesignDiagnosticSeverity.Attention);

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
                    || !静的適格性判定器.クラスを静的化できる(宣言, シンボル, 意味モデル)
                )
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    "This class has no meaningful instance state or polymorphic role and can be static.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }
}
