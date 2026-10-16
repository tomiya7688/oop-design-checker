using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class カプセル化漏出規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP106", "Encapsulation leak", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (var 診断 in フィールドを解析する(構文ルート, 意味モデル))
            {
                yield return 診断;
            }

            foreach (
                var プロパティ in 構文ルート.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            )
            {
                if (
                    意味モデル.GetDeclaredSymbol(プロパティ) is not IPropertySymbol プロパティ記号
                    || !宣言型の外部に公開される(プロパティ記号.ContainingType)
                    || データ保持型判定器.明示的データ保持型である(プロパティ記号.ContainingType)
                )
                {
                    continue;
                }

                if (可変な保持値を公開する(プロパティ, プロパティ記号, 意味モデル))
                {
                    yield return 診断生成器.生成する(
                        規則情報,
                        プロパティ.Identifier.GetLocation(),
                        "This property exposes mutable internal storage directly. Prefer a read-only view, copy, or behavior-oriented API.",
                        プロパティ記号.ToDisplayString(),
                        DesignDiagnosticSeverity.Danger
                    );
                }

                if (不要に公開された設定子がある(文脈, プロパティ, プロパティ記号))
                {
                    yield return 診断生成器.生成する(
                        規則情報,
                        プロパティ.Identifier.GetLocation(),
                        "This public setter is only used by the declaring object. Narrow the setter visibility.",
                        プロパティ記号.ToDisplayString()
                    );
                }
            }
        }
    }

    private IEnumerable<DesignDiagnostic> フィールドを解析する(
        SyntaxNode 構文ルート,
        SemanticModel 意味モデル
    )
    {
        foreach (var 変数 in 構文ルート.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (変数.Parent?.Parent is not FieldDeclarationSyntax)
            {
                continue;
            }

            if (
                意味モデル.GetDeclaredSymbol(変数) is not IFieldSymbol フィールド
                || フィールド.DeclaredAccessibility != Accessibility.Public
                || フィールド.IsConst
                || !宣言型の外部に公開される(フィールド.ContainingType)
                || データ保持型判定器.明示的データ保持型である(フィールド.ContainingType)
            )
            {
                continue;
            }

            var 重要度 = フィールド.IsReadOnly
                ? DesignDiagnosticSeverity.Warning
                : DesignDiagnosticSeverity.Danger;
            var 診断文 = フィールド.IsReadOnly
                ? "A public field exposes the object's representation. Prefer a property or behavior-oriented API."
                : "A public mutable field allows callers to bypass encapsulation.";

            yield return 診断生成器.生成する(
                規則情報,
                変数.GetLocation(),
                診断文,
                フィールド.ToDisplayString(),
                重要度
            );
        }
    }

    private static bool 宣言型の外部に公開される(INamedTypeSymbol 型)
    {
        for (var 現在の項目 = 型; 現在の項目 is not null; 現在の項目 = 現在の項目.ContainingType)
        {
            if (現在の項目.DeclaredAccessibility == Accessibility.Private)
            {
                return false;
            }
        }

        return true;
    }

    private static bool 可変な保持値を公開する(
        PropertyDeclarationSyntax 宣言,
        IPropertySymbol プロパティ,
        SemanticModel 意味モデル
    )
    {
        if (
            プロパティ.GetMethod?.DeclaredAccessibility != Accessibility.Public
            || !可変コレクション検査器.可変コレクションである(プロパティ.Type)
        )
        {
            return false;
        }

        if (宣言.ExpressionBody is not null)
        {
            return 保持値を参照する(宣言.ExpressionBody.Expression, 意味モデル);
        }

        var 取得子 = 宣言.AccessorList?.Accessors.FirstOrDefault(アクセサ =>
            アクセサ.IsKind(SyntaxKind.GetAccessorDeclaration)
        );
        if (取得子 is null)
        {
            return false;
        }

        if (取得子.Body is null && 取得子.ExpressionBody is null)
        {
            return true;
        }

        if (取得子.ExpressionBody is not null)
        {
            return 保持値を参照する(取得子.ExpressionBody.Expression, 意味モデル);
        }

        if (
            取得子.Body?.Statements
            is [ReturnStatementSyntax { Expression: not null } returnStatement]
        )
        {
            return 保持値を参照する(returnStatement.Expression, 意味モデル);
        }

        return false;
    }

    private static bool 保持値を参照する(ExpressionSyntax expression, SemanticModel 意味モデル) =>
        意味モデル.GetSymbolInfo(expression).Symbol
            is IFieldSymbol { IsStatic: false }
                or IPropertySymbol { IsStatic: false };

    private static bool 不要に公開された設定子がある(
        解析文脈 文脈,
        PropertyDeclarationSyntax 宣言,
        IPropertySymbol プロパティ
    )
    {
        if (
            !文脈.対象プロジェクト.アプリケーションである
            || プロパティ.SetMethod?.DeclaredAccessibility != Accessibility.Public
            || プロパティ.IsAbstract
            || プロパティ.IsVirtual
            || プロパティ.IsOverride
            || プロパティ.ContainingType.TypeKind == TypeKind.Interface
            || インターフェースプロパティを実装する(プロパティ)
            || 宣言.AccessorList?.Accessors.Any(アクセサ =>
                アクセサ.IsKind(SyntaxKind.InitAccessorDeclaration)
            ) == true
        )
        {
            return false;
        }

        var 書き込み範囲 = プロパティ書き込み検査器.書き込み範囲を調べる(文脈, プロパティ);
        return 書き込み範囲.内部書き込みがある && !書き込み範囲.外部書き込みがある;
    }

    private static bool インターフェースプロパティを実装する(IPropertySymbol プロパティ)
    {
        foreach (var インターフェース型 in プロパティ.ContainingType.AllInterfaces)
        {
            foreach (
                var インターフェースプロパティ in インターフェース型
                    .GetMembers()
                    .OfType<IPropertySymbol>()
            )
            {
                var 実装記号 = プロパティ.ContainingType.FindImplementationForInterfaceMember(
                    インターフェースプロパティ
                );
                if (SymbolEqualityComparer.Default.Equals(実装記号, プロパティ))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
