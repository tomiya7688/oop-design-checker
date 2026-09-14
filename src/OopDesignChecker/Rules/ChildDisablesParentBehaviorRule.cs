using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ChildDisablesParentBehaviorRule : IAnalysisRule
{
    private static readonly HashSet<string> RejectionExceptions = new(StringComparer.Ordinal)
    {
        "NotSupportedException",
        "System.NotSupportedException",
        "NotImplementedException",
        "System.NotImplementedException"
    };

    public RuleDescriptor Descriptor { get; } = new(
        "OOP303",
        "Child disables parent behavior",
        DesignDiagnosticSeverity.Error);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol { IsOverride: true } methodSymbol)
                {
                    continue;
                }

                var thrownType = GetSoleRejectionException(method);
                if (thrownType is null)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    method.Identifier.GetLocation(),
                    $"This override disables inherited behavior by always throwing {thrownType}.",
                    methodSymbol.ToDisplayString());
            }
        }
    }

    private static string? GetSoleRejectionException(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody?.Expression is ThrowExpressionSyntax throwExpression)
        {
            return GetExceptionName(throwExpression.Expression);
        }

        if (method.Body?.Statements is [ThrowStatementSyntax throwStatement])
        {
            return GetExceptionName(throwStatement.Expression);
        }

        return null;
    }

    private static string? GetExceptionName(ExpressionSyntax? expression)
    {
        if (expression is not ObjectCreationExpressionSyntax creation)
        {
            return null;
        }

        var typeName = creation.Type.ToString();
        return RejectionExceptions.Contains(typeName) ? typeName : null;
    }
}
