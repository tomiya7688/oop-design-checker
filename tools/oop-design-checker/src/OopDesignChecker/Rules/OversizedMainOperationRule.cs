using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class OversizedMainOperationRule : IAnalysisRule
{
    private const int MinimumStatementCount = 20;
    private const int WarningScore = 4;

    public RuleDescriptor Descriptor { get; } =
        new("OOP201", "Oversized main operation", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol
                    || IsFrameworkManaged(methodSymbol)
                )
                {
                    continue;
                }

                var metrics = OperationMetricsCalculator.Calculate(
                    method,
                    methodSymbol,
                    semanticModel
                );
                if (
                    metrics.StatementCount < MinimumStatementCount
                    || CalculateOversizeScore(metrics) < WarningScore
                    || IsPrivateHelper(context, methodSymbol)
                )
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    method.Identifier.GetLocation(),
                    $"This primary operation is structurally large: {metrics.LineCount} lines, {metrics.StatementCount} statements, complexity {metrics.CyclomaticComplexity}, nesting {metrics.MaxNestingDepth}. Split meaningful processing phases into methods.",
                    methodSymbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsFrameworkManaged(IMethodSymbol method) =>
        FrameworkContractClassifier.HasExternalBaseTypeContract(method.ContainingType)
        || FrameworkContractClassifier.HasExternalFrameworkAttribute(method.ContainingType)
        || FrameworkContractClassifier.HasExternalFrameworkAttribute(method);

    private static int CalculateOversizeScore(OperationMetrics metrics)
    {
        var score = 0;

        score += metrics.LineCount switch
        {
            >= 120 => 2,
            >= 70 => 1,
            _ => 0,
        };
        score += metrics.CyclomaticComplexity switch
        {
            >= 18 => 2,
            >= 10 => 1,
            _ => 0,
        };
        score += metrics.MaxNestingDepth switch
        {
            >= 4 => 2,
            >= 3 => 1,
            _ => 0,
        };

        if (metrics.StatementCount >= 40)
        {
            score++;
        }

        if (metrics.LocalVariableCount >= 10)
        {
            score++;
        }

        if (metrics.StateMemberCount >= 5)
        {
            score++;
        }

        if (metrics.TopLevelStatementCount >= 15)
        {
            score++;
        }

        return score;
    }

    private static bool IsPrivateHelper(AnalysisContext context, IMethodSymbol target)
    {
        if (target.DeclaredAccessibility != Accessibility.Private)
        {
            return false;
        }

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (
                var invocation in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
            )
            {
                if (
                    semanticModel.GetSymbolInfo(invocation).Symbol
                        is not IMethodSymbol invokedMethod
                    || !SymbolEqualityComparer.Default.Equals(
                        invokedMethod.OriginalDefinition,
                        target.OriginalDefinition
                    )
                )
                {
                    continue;
                }

                var callerDeclaration = invocation
                    .Ancestors()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();
                if (
                    callerDeclaration is not null
                    && semanticModel.GetDeclaredSymbol(callerDeclaration) is IMethodSymbol caller
                    && !SymbolEqualityComparer.Default.Equals(
                        caller.OriginalDefinition,
                        target.OriginalDefinition
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }
}
