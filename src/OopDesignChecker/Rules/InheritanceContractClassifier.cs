using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Rules;

internal enum ParentOperationContract
{
    Unknown,
    Required,
    Supported,
    OptionalNoOp,
    Rejected,
}

internal static class InheritanceContractClassifier
{
    public static ParentOperationContract ClassifyParentOperation(IMethodSymbol overrideMethod)
    {
        var parentMethod = overrideMethod.OverriddenMethod;
        if (parentMethod is null)
        {
            return ParentOperationContract.Unknown;
        }

        if (parentMethod.IsAbstract)
        {
            return ParentOperationContract.Required;
        }

        if (!parentMethod.Locations.Any(location => location.IsInSource))
        {
            return ParentOperationContract.Unknown;
        }

        var declaration = parentMethod
            .DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        if (declaration is null || declaration.Body is null && declaration.ExpressionBody is null)
        {
            return ParentOperationContract.Unknown;
        }

        if (GetSoleRejectionException(declaration) is not null)
        {
            return ParentOperationContract.Rejected;
        }

        return IsNoOpOrDefaultImplementation(declaration, parentMethod)
            ? ParentOperationContract.OptionalNoOp
            : ParentOperationContract.Supported;
    }

    public static bool IsNoOpOrDefaultImplementation(
        MethodDeclarationSyntax declaration,
        IMethodSymbol method
    )
    {
        if (method.ReturnsVoid)
        {
            return declaration.Body is { Statements.Count: 0 }
                || declaration.Body?.Statements is [ReturnStatementSyntax { Expression: null }];
        }

        if (declaration.ExpressionBody is { Expression: { } expression })
        {
            return IsDefaultValueExpression(expression);
        }

        return declaration.Body?.Statements
                is [ReturnStatementSyntax { Expression: { } returnExpression }]
            && IsDefaultValueExpression(returnExpression);
    }

    public static string? GetSoleRejectionException(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody?.Expression is ThrowExpressionSyntax throwExpression)
        {
            return GetRejectionExceptionName(throwExpression.Expression);
        }

        if (method.Body?.Statements is [ThrowStatementSyntax throwStatement])
        {
            return GetRejectionExceptionName(throwStatement.Expression);
        }

        return null;
    }

    private static bool IsDefaultValueExpression(ExpressionSyntax expression) =>
        expression is DefaultExpressionSyntax
        || expression.IsKind(SyntaxKind.NullLiteralExpression)
        || expression.IsKind(SyntaxKind.DefaultLiteralExpression);

    private static string? GetRejectionExceptionName(ExpressionSyntax? expression)
    {
        if (expression is not ObjectCreationExpressionSyntax creation)
        {
            return null;
        }

        var typeName = creation.Type.ToString();
        return
            typeName
                is "NotSupportedException"
                    or "System.NotSupportedException"
                    or "global::System.NotSupportedException"
                    or "NotImplementedException"
                    or "System.NotImplementedException"
                    or "global::System.NotImplementedException"
            ? typeName
            : null;
    }
}
