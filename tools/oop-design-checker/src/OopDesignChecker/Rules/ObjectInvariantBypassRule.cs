using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 不変条件迂回規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP107", "Object invariant can be bypassed", DesignDiagnosticSeverity.Danger);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (
                var クラス宣言 in 構文ルート.DescendantNodes().OfType<ClassDeclarationSyntax>()
            )
            {
                if (
                    意味モデル.GetDeclaredSymbol(クラス宣言) is INamedTypeSymbol クラス記号
                    && データ保持型判定器.明示的データ保持型である(クラス記号)
                )
                {
                    continue;
                }

                foreach (
                    var プロパティ宣言 in クラス宣言.Members.OfType<PropertyDeclarationSyntax>()
                )
                {
                    if (
                        意味モデル.GetDeclaredSymbol(プロパティ宣言)
                            is not IPropertySymbol プロパティ
                        || プロパティ.SetMethod?.DeclaredAccessibility != Accessibility.Public
                        || プロパティ.IsStatic
                        || 設定子が入力値を検証する(プロパティ宣言, プロパティ, 意味モデル)
                    )
                    {
                        continue;
                    }

                    var 検証付き変更メソッド = クラス宣言
                        .Members.OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(メソッド =>
                            検証後に代入する(メソッド, プロパティ, 意味モデル)
                        );
                    if (検証付き変更メソッド is null)
                    {
                        continue;
                    }

                    yield return 診断生成器.生成する(
                        規則情報,
                        プロパティ宣言.Identifier.GetLocation(),
                        $"{プロパティ.Name} has a public setter even though {検証付き変更メソッド.Identifier.ValueText} validates values before assigning it. Callers can bypass the object's invariant through the setter.",
                        プロパティ.ToDisplayString()
                    );
                }
            }
        }
    }

    private static bool 設定子が入力値を検証する(
        PropertyDeclarationSyntax 宣言,
        IPropertySymbol プロパティ,
        SemanticModel 意味モデル
    )
    {
        var 設定子 = 宣言.AccessorList?.Accessors.FirstOrDefault(アクセサ =>
            アクセサ.IsKind(SyntaxKind.SetAccessorDeclaration)
        );
        if (
            設定子 is null
            || プロパティ.SetMethod is not IMethodSymbol 設定子記号
            || 設定子記号.Parameters.Length != 1
        )
        {
            return false;
        }

        var 値引数記号 = 設定子記号.Parameters[0];
        return 設定子.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(条件分岐構文 =>
                引数を参照する(条件分岐構文.Condition, 値引数記号, 意味モデル)
                && 条件分岐構文
                    .Statement.DescendantNodesAndSelf()
                    .OfType<ThrowStatementSyntax>()
                    .Any()
            );
    }

    private static bool 検証後に代入する(
        MethodDeclarationSyntax メソッド,
        IPropertySymbol プロパティ,
        SemanticModel 意味モデル
    )
    {
        if (
            意味モデル.GetDeclaredSymbol(メソッド) is not IMethodSymbol メソッド記号
            || メソッド記号.IsStatic
        )
        {
            return false;
        }

        foreach (var 代入式 in メソッド.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (
                !SymbolEqualityComparer.Default.Equals(
                    意味モデル.GetSymbolInfo(代入式.Left).Symbol,
                    プロパティ
                ) || 意味モデル.GetSymbolInfo(代入式.Right).Symbol is not IParameterSymbol 引数
            )
            {
                continue;
            }

            if (
                メソッド.DescendantNodes()
                    .OfType<IfStatementSyntax>()
                    .Any(条件分岐構文 =>
                        引数を参照する(条件分岐構文.Condition, 引数, 意味モデル)
                        && 条件分岐構文
                            .Statement.DescendantNodesAndSelf()
                            .OfType<ThrowStatementSyntax>()
                            .Any()
                    )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool 引数を参照する(
        ExpressionSyntax expression,
        IParameterSymbol 引数,
        SemanticModel 意味モデル
    ) =>
        expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(識別子 =>
                SymbolEqualityComparer.Default.Equals(意味モデル.GetSymbolInfo(識別子).Symbol, 引数)
            );
}
