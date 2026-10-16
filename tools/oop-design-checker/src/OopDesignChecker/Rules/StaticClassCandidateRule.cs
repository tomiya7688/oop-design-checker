using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class StaticClassCandidateRule : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP104", "Static class candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                    || !StaticEligibilityEvaluator.CanClassBeStatic(
                        declaration,
                        symbol,
                        semanticModel
                    )
                )
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    declaration.Identifier.GetLocation(),
                    "This class has no meaningful instance state or polymorphic role and can be static.",
                    symbol.ToDisplayString()
                );
            }
        }
    }
}
