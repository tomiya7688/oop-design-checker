using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Rules;

internal enum 親操作契約
{
    不明,
    必須,
    実装済み,
    任意の空実装,
    拒否済み,
}

internal static class 継承契約判定器
{
    public static 親操作契約 親操作契約を分類する(IMethodSymbol overrideMethod)
    {
        var 親メソッド = overrideMethod.OverriddenMethod;
        if (親メソッド is null)
        {
            return 親操作契約.不明;
        }

        if (親メソッド.IsAbstract)
        {
            return 親操作契約.必須;
        }

        if (!親メソッド.Locations.Any(location => location.IsInSource))
        {
            return 親操作契約.不明;
        }

        var 宣言 = 親メソッド
            .DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        if (宣言 is null || 宣言.Body is null && 宣言.ExpressionBody is null)
        {
            return 親操作契約.不明;
        }

        if (単独の拒否例外を取得する(宣言) is not null)
        {
            return 親操作契約.拒否済み;
        }

        return 空実装または既定実装である(宣言, 親メソッド)
            ? 親操作契約.任意の空実装
            : 親操作契約.実装済み;
    }

    public static bool 空実装または既定実装である(
        MethodDeclarationSyntax 宣言,
        IMethodSymbol メソッド
    )
    {
        if (メソッド.ReturnsVoid)
        {
            return 宣言.Body is { Statements.Count: 0 }
                || 宣言.Body?.Statements is [ReturnStatementSyntax { Expression: null }];
        }

        if (宣言.ExpressionBody is { Expression: { } 式 })
        {
            return 既定値式である(式);
        }

        return 宣言.Body?.Statements is [ReturnStatementSyntax { Expression: { } returnExpression }]
            && 既定値式である(returnExpression);
    }

    public static string? 単独の拒否例外を取得する(MethodDeclarationSyntax メソッド)
    {
        if (メソッド.ExpressionBody?.Expression is ThrowExpressionSyntax throwExpression)
        {
            return 拒否例外名を取得する(throwExpression.Expression);
        }

        if (メソッド.Body?.Statements is [ThrowStatementSyntax throwStatement])
        {
            return 拒否例外名を取得する(throwStatement.Expression);
        }

        return null;
    }

    private static bool 既定値式である(ExpressionSyntax 式) =>
        式 is DefaultExpressionSyntax
        || 式.IsKind(SyntaxKind.NullLiteralExpression)
        || 式.IsKind(SyntaxKind.DefaultLiteralExpression);

    private static string? 拒否例外名を取得する(ExpressionSyntax? 式)
    {
        if (式 is not ObjectCreationExpressionSyntax 生成式)
        {
            return null;
        }

        var 型名 = 生成式.Type.ToString();
        return
            型名
                is "NotSupportedException"
                    or "System.NotSupportedException"
                    or "global::System.NotSupportedException"
                    or "NotImplementedException"
                    or "System.NotImplementedException"
                    or "global::System.NotImplementedException"
            ? 型名
            : null;
    }
}
