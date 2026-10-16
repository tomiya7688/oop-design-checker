using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 継承深度過剰規則 : I解析規則
{
    private readonly int _警告深度;

    public 継承深度過剰規則(int 警告深度 = 4)
    {
        if (警告深度 < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(警告深度),
                警告深度,
                "Inheritance warning depth must be at least 1."
            );
        }

        _警告深度 = 警告深度;
    }

    public RuleDescriptor 規則情報 { get; } =
        new("OOP304", "Excessive inheritance depth", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 宣言 in 構文根.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol シンボル
                    || !シンボル操作器.主宣言である(シンボル, 宣言)
                )
                {
                    continue;
                }

                var 継承深度 = シンボル操作器.継承深度を取得する(
                    シンボル,
                    解析文脈.対象プロジェクト群
                );
                if (継承深度 < _警告深度)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"Project-owned inheritance depth is {継承深度} (configured attention threshold: {_警告深度}). Deep inheritance can make behavior harder to reason about.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }
}
