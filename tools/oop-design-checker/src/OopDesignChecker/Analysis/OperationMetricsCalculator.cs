using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class OperationMetricsCalculator
{
    public static OperationMetrics Calculate(
        MethodDeclarationSyntax method,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel
    )
    {
        var operationNodes = MethodComplexityCalculator.GetOperationNodes(method).ToArray();
        var lineSpan = method.GetLocation().GetLineSpan();
        var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        var statementCount = operationNodes
            .OfType<StatementSyntax>()
            .Count(statement => statement is not LocalFunctionStatementSyntax);
        var maxNestingDepth = operationNodes
            .Where(IsNestingNode)
            .Select(node => GetNestingDepth(node, method))
            .DefaultIfEmpty(0)
            .Max();
        var localVariableCount =
            operationNodes.OfType<VariableDeclaratorSyntax>().Count()
            + operationNodes.OfType<ForEachStatementSyntax>().Count();
        var stateMemberCount = CountStateMembers(
            operationNodes,
            methodSymbol.ContainingType,
            semanticModel
        );
        var topLevelStatementCount =
            method.Body?.Statements.Count(statement =>
                statement is not LocalFunctionStatementSyntax
            ) ?? (method.ExpressionBody is null ? 0 : 1);

        return new OperationMetrics(
            lineCount,
            statementCount,
            MethodComplexityCalculator.Calculate(method),
            maxNestingDepth,
            localVariableCount,
            stateMemberCount,
            topLevelStatementCount
        );
    }

    private static int CountStateMembers(
        IReadOnlyList<SyntaxNode> operationNodes,
        INamedTypeSymbol containingType,
        SemanticModel semanticModel
    )
    {
        var stateMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var identifier in operationNodes.OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (
                symbol is IFieldSymbol { IsStatic: false } field
                && BelongsToHierarchy(field.ContainingType, containingType)
            )
            {
                stateMembers.Add(field);
                continue;
            }

            if (
                symbol is IPropertySymbol { IsStatic: false } property
                && BelongsToHierarchy(property.ContainingType, containingType)
            )
            {
                stateMembers.Add(property);
            }
        }

        return stateMembers.Count;
    }

    private static bool BelongsToHierarchy(
        INamedTypeSymbol? declaringType,
        INamedTypeSymbol containingType
    )
    {
        for (var current = containingType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, declaringType))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetNestingDepth(SyntaxNode node, MethodDeclarationSyntax method)
    {
        var depth = 1;
        foreach (var ancestor in node.Ancestors())
        {
            if (ReferenceEquals(ancestor, method))
            {
                break;
            }

            if (IsNestingNode(ancestor))
            {
                depth++;
            }
        }

        return depth;
    }

    private static bool IsNestingNode(SyntaxNode node) =>
        node
            is IfStatementSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or SwitchStatementSyntax
                or TryStatementSyntax
                or CatchClauseSyntax
                or LockStatementSyntax
                or UsingStatementSyntax;
}
