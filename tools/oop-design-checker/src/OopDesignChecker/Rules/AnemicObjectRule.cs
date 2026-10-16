using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 貧血オブジェクト規則 : I解析規則
{
    private const int 最小状態プロパティ数 = 3;
    private const int 最小外部使用メンバー数 = 3;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP402", "Anemic object candidate", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (var 宣言 in 構文ルート.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol 記号
                    || データ保持型判定器.明示的データ保持型である(記号)
                    || 公開状態プロパティ数を数える(宣言) < 最小状態プロパティ数
                    || 公開振る舞いメソッド数を数える(宣言, 意味モデル) > 1
                )
                {
                    continue;
                }

                var 外部振る舞い = 外部の振る舞いを探す(文脈, 記号);
                if (外部振る舞い is null)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"{記号.Name} mostly exposes state while {外部振る舞い.Name} performs behavior by directly using several of its members. Consider moving state-owning behavior into the object.",
                    記号.ToDisplayString()
                );
            }
        }
    }

    private static int 公開状態プロパティ数を数える(ClassDeclarationSyntax 宣言) =>
        宣言
            .Members.OfType<PropertyDeclarationSyntax>()
            .Count(プロパティ =>
                プロパティ.Modifiers.Any(SyntaxKind.PublicKeyword)
                && プロパティ.AccessorList is not null
                && プロパティ.AccessorList.Accessors.All(アクセサ =>
                    アクセサ.Body is null && アクセサ.ExpressionBody is null
                )
            );

    private static int 公開振る舞いメソッド数を数える(
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    ) =>
        宣言
            .Members.OfType<MethodDeclarationSyntax>()
            .Count(メソッド =>
                意味モデル.GetDeclaredSymbol(メソッド)
                    is IMethodSymbol
                    {
                        IsStatic: false,
                        DeclaredAccessibility: Accessibility.Public,
                        MethodKind: MethodKind.Ordinary,
                    }
            );

    private static IMethodSymbol? 外部の振る舞いを探す(解析文脈 文脈, INamedTypeSymbol 候補型)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var メソッド in 構文木.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            )
            {
                if (
                    意味モデル.GetDeclaredSymbol(メソッド) is not IMethodSymbol メソッド記号
                    || SymbolEqualityComparer.Default.Equals(メソッド記号.ContainingType, 候補型)
                    || 投影用基盤である(メソッド記号.ContainingType)
                )
                {
                    continue;
                }

                foreach (var 引数 in メソッド.ParameterList.Parameters)
                {
                    if (
                        意味モデル.GetDeclaredSymbol(引数) is not IParameterSymbol 引数記号
                        || !SymbolEqualityComparer.Default.Equals(引数記号.Type, 候補型)
                    )
                    {
                        continue;
                    }

                    var メンバー群 = メソッド.DescendantNodes()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Where(access => 受信先が引数である(access, 引数記号, 意味モデル))
                        .Select(access => 意味モデル.GetSymbolInfo(access).Symbol)
                        .Where(メンバー =>
                            SymbolEqualityComparer.Default.Equals(メンバー?.ContainingType, 候補型)
                        )
                        .Select(メンバー => メンバー!.Name)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (メンバー群.Length >= 最小外部使用メンバー数)
                    {
                        return メソッド記号;
                    }
                }
            }
        }

        return null;
    }

    private static bool 受信先が引数である(
        MemberAccessExpressionSyntax access,
        IParameterSymbol 引数,
        SemanticModel 意味モデル
    ) =>
        SymbolEqualityComparer.Default.Equals(
            意味モデル.GetSymbolInfo(access.Expression).Symbol,
            引数
        );

    private static bool 投影用基盤である(INamedTypeSymbol 型)
    {
        var 型名 = 型.Name;
        return 型名.EndsWith("Mapper", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Formatter", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Serializer", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Converter", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Projection", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Presenter", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Factory", StringComparison.OrdinalIgnoreCase);
    }
}
