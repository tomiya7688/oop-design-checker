using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class 処理量指標計算器
{
    public static 処理量指標 計算する(
        MethodDeclarationSyntax method,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel
    )
    {
        var operationNodes = メソッド複雑度計算器.処理構文ノード群を取得する(method).ToArray();
        var lineSpan = method.GetLocation().GetLineSpan();
        var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        var statementCount = operationNodes
            .OfType<StatementSyntax>()
            .Count(statement => statement is not LocalFunctionStatementSyntax);
        var maxNestingDepth = operationNodes
            .Where(ネスト構造である)
            .Select(node => ネスト深度を数える(node, method))
            .DefaultIfEmpty(0)
            .Max();
        var localVariableCount =
            operationNodes.OfType<VariableDeclaratorSyntax>().Count()
            + operationNodes.OfType<ForEachStatementSyntax>().Count();
        var stateMemberCount = 状態メンバー数を数える(
            operationNodes,
            methodSymbol.ContainingType,
            semanticModel
        );
        var topLevel文数 =
            method.Body?.Statements.Count(statement =>
                statement is not LocalFunctionStatementSyntax
            ) ?? (method.ExpressionBody is null ? 0 : 1);

        return new 処理量指標(
            lineCount,
            statementCount,
            メソッド複雑度計算器.計算する(method),
            maxNestingDepth,
            localVariableCount,
            stateMemberCount,
            topLevel文数
        );
    }

    private static int 状態メンバー数を数える(
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
                && 継承階層に属する(field.ContainingType, containingType)
            )
            {
                stateMembers.Add(field);
                continue;
            }

            if (
                symbol is IPropertySymbol { IsStatic: false } property
                && 継承階層に属する(property.ContainingType, containingType)
            )
            {
                stateMembers.Add(property);
            }
        }

        return stateMembers.Count;
    }

    private static bool 継承階層に属する(
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

    private static int ネスト深度を数える(SyntaxNode node, MethodDeclarationSyntax method)
    {
        var depth = 1;
        foreach (var ancestor in node.Ancestors())
        {
            if (ReferenceEquals(ancestor, method))
            {
                break;
            }

            if (ネスト構造である(ancestor))
            {
                depth++;
            }
        }

        return depth;
    }

    private static bool ネスト構造である(SyntaxNode node) =>
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
