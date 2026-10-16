using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 子型による親動作無効化規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP303", "Child disables parent behavior", DesignDiagnosticSeverity.Danger);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var メソッド in 構文根.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(メソッド)
                    is not IMethodSymbol { IsOverride: true } methodSymbol
                )
                {
                    continue;
                }

                var 例外型 = 継承契約判定器.単独の拒否例外を取得する(メソッド);
                if (例外型 is null || !親の実装済み操作を無効化する(methodSymbol))
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    メソッド.Identifier.GetLocation(),
                    $"This override disables required or supported inherited behavior by always throwing {例外型}.",
                    methodSymbol.ToDisplayString()
                );
            }
        }
    }

    private static bool 親の実装済み操作を無効化する(IMethodSymbol メソッド) =>
        継承契約判定器.親操作契約を分類する(メソッド)
            is 親操作契約.必須
                or 親操作契約.実装済み
                or 親操作契約.任意の空実装;
}
