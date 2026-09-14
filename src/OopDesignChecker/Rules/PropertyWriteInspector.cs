using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Rules;

internal static class PropertyWriteInspector
{
    public static PropertyWriteScope GetWriteScope(
        AnalysisContext context,
        IPropertySymbol property
    )
    {
        var hasInternalWrite = false;
        var hasExternalWrite = false;

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!IsTargetProperty(semanticModel, assignment.Left, property))
                {
                    continue;
                }

                ClassifyWrite(
                    semanticModel,
                    assignment.SpanStart,
                    property,
                    ref hasInternalWrite,
                    ref hasExternalWrite
                );
            }

            foreach (var unary in root.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
            {
                if (IsTargetProperty(semanticModel, unary.Operand, property))
                {
                    ClassifyWrite(
                        semanticModel,
                        unary.SpanStart,
                        property,
                        ref hasInternalWrite,
                        ref hasExternalWrite
                    );
                }
            }

            foreach (var unary in root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
            {
                if (IsTargetProperty(semanticModel, unary.Operand, property))
                {
                    ClassifyWrite(
                        semanticModel,
                        unary.SpanStart,
                        property,
                        ref hasInternalWrite,
                        ref hasExternalWrite
                    );
                }
            }
        }

        return new PropertyWriteScope(hasInternalWrite, hasExternalWrite);
    }

    private static bool IsTargetProperty(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        IPropertySymbol property
    ) =>
        SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSymbolInfo(expression).Symbol,
            property
        );

    private static void ClassifyWrite(
        SemanticModel semanticModel,
        int position,
        IPropertySymbol property,
        ref bool hasInternalWrite,
        ref bool hasExternalWrite
    )
    {
        var writingType = semanticModel.GetEnclosingSymbol(position)?.ContainingType;
        if (SymbolEqualityComparer.Default.Equals(writingType, property.ContainingType))
        {
            hasInternalWrite = true;
        }
        else
        {
            hasExternalWrite = true;
        }
    }
}

internal readonly record struct PropertyWriteScope(bool HasInternalWrite, bool HasExternalWrite);
