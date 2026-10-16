using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ParentContractMostlyUnusedRule : I解析規則
{
    private const int MinimumNoOpOverrides = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP302", "Parent contract mostly unused", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !シンボル操作器.主宣言である(symbol, declaration)
                )
                {
                    continue;
                }

                var neutralizedOperations = declaration
                    .Members.OfType<MethodDeclarationSyntax>()
                    .Where(method => NeutralizesParentContract(method, semanticModel))
                    .ToArray();
                if (neutralizedOperations.Length < MinimumNoOpOverrides)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    declaration.Identifier.GetLocation(),
                    $"This child type neutralizes {neutralizedOperations.Length} required or supported inherited operations with no-op/default implementations. The parent contract may be broader than this object can meaningfully support.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool NeutralizesParentContract(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        if (
            semanticModel.GetDeclaredSymbol(declaration)
                is not IMethodSymbol { IsOverride: true } method
            || !InheritanceContractClassifier.IsNoOpOrDefaultImplementation(declaration, method)
        )
        {
            return false;
        }

        return InheritanceContractClassifier.ClassifyParentOperation(method)
            is ParentOperationContract.Required
                or ParentOperationContract.Supported;
    }
}
