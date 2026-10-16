using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class メソッド複雑度計算器
{
    public static int 計算する(MethodDeclarationSyntax method)
    {
        var complexity = 1;

        foreach (var node in 処理構文ノード群を取得する(method))
        {
            complexity += node switch
            {
                IfStatementSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                WhileStatementSyntax => 1,
                DoStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                CasePatternSwitchLabelSyntax => 1,
                SwitchExpressionArmSyntax => 1,
                CatchClauseSyntax => 1,
                ConditionalExpressionSyntax => 1,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) =>
                    1,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression) =>
                    1,
                _ => 0,
            };
        }

        return complexity;
    }

    internal static IEnumerable<SyntaxNode> 処理構文ノード群を取得する(
        MethodDeclarationSyntax method
    ) =>
        method.DescendantNodes(descendIntoChildren: node =>
            node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax
        );
}
