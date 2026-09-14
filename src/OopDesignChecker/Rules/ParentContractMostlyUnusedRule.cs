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

                var noOpOverrides = declaration
                    .Members.OfType<MethodDeclarationSyntax>()
                    .Where(method => IsNoOpOverride(method, semanticModel))
                    .ToArray();
                if (noOpOverrides.Length < MinimumNoOpOverrides)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This child type neutralizes {noOpOverrides.Length} inherited operations with no-op/default implementations. The parent contract may be broader than this object can meaningfully support.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsNoOpOverride(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        if (
            semanticModel.GetDeclaredSymbol(declaration)
            is not IMethodSymbol { IsOverride: true } method
        )
        {
            return false;
        }

        if (method.ReturnsVoid)
        {
            return declaration.Body is { Statements.Count: 0 }
                || declaration.Body?.Statements is [ReturnStatementSyntax { Expression: null }];
        }

        if (declaration.ExpressionBody?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression)
                || literal.IsKind(
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.DefaultLiteralExpression
                );
        }

        return declaration.Body?.Statements
                is [ReturnStatementSyntax { Expression: LiteralExpressionSyntax returnLiteral }]
            && (
                returnLiteral.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression)
                || returnLiteral.IsKind(
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.DefaultLiteralExpression
                )
            );
    }
}
