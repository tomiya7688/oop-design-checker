using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveInheritanceDepthRule : I解析規則
{
    private readonly int _warningDepth;

    public ExcessiveInheritanceDepthRule(int warningDepth = 4)
    {
        if (warningDepth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warningDepth),
                warningDepth,
                "Inheritance warning depth must be at least 1."
            );
        }

        _warningDepth = warningDepth;
    }

    public RuleDescriptor 規則情報 { get; } =
        new("OOP304", "Excessive inheritance depth", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !シンボル操作器.主宣言である(symbol, declaration)
                )
                {
                    continue;
                }

                var depth = シンボル操作器.継承深度を取得する(symbol, context.対象プロジェクト群);
                if (depth < _warningDepth)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    declaration.Identifier.GetLocation(),
                    $"Project-owned inheritance depth is {depth} (configured attention threshold: {_warningDepth}). Deep inheritance can make behavior harder to reason about.",
                    symbol.ToDisplayString()
                );
            }
        }
    }
}
