using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ParentContractMostlyUnusedRule : IAnalysisRule
{
    private const int MinimumNoOpOverrides = 2;

    public RuleDescriptor Descriptor { get; } =
        new("OOP302", "Parent contract mostly unused", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
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

                yield return DiagnosticFactory.Create(
                    Descriptor,
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
            is ParentOperationContract.Required or ParentOperationContract.Supported;
    }
}
