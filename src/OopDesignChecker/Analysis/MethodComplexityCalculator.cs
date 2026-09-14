using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class MethodComplexityCalculator
{
    public static int Calculate(MethodDeclarationSyntax method)
    {
        var complexity = 1;

        foreach (var node in method.DescendantNodes())
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
                CatchClauseSyntax => 1,
                ConditionalExpressionSyntax => 1,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) => 1,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression) => 1,
                _ => 0
            };
        }

        return complexity;
    }
}
