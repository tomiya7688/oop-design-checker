using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveObjectNavigationRule : IAnalysisRule
{
    private const int MinimumNavigationDepth = 3;

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP404",
            "Excessive navigation through object internals",
            DesignDiagnosticSeverity.Attention
        );

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax methodAccess)
                {
                    continue;
                }

                var navigation = ReadNavigationChain(methodAccess.Expression, semanticModel).ToArray();
                if (navigation.Length < MinimumNavigationDepth)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    invocation.GetLocation(),
                    $"This call traverses {navigation.Length} instance state boundaries before invoking behavior. Repeated deep navigation couples the caller to another object's internal graph."
                );
            }
        }
    }

    private static IEnumerable<ISymbol> ReadNavigationChain(
        ExpressionSyntax expression,
        SemanticModel semanticModel
    )
    {
        var current = expression;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (symbol is not IPropertySymbol { IsStatic: false }
                and not IFieldSymbol { IsStatic: false })
            {
                yield break;
            }

            yield return symbol;
            current = memberAccess.Expression;
        }
    }
}
