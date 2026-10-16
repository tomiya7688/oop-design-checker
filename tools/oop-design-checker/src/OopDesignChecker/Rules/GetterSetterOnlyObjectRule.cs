using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 取得設定のみオブジェクト規則 : I解析規則
{
    private const int 最小状態プロパティ数 = 4;
    private const int 最大振る舞いメソッド数 = 1;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP405", "Getter/setter-only object candidate", DesignDiagnosticSeverity.Attention);

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
                )
                {
                    continue;
                }

                var 状態プロパティ群 = 宣言
                    .Members.OfType<PropertyDeclarationSyntax>()
                    .Where(単純な公開状態プロパティである)
                    .ToArray();
                if (状態プロパティ群.Length < 最小状態プロパティ数)
                {
                    continue;
                }

                var 振る舞い数 = 宣言
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
                if (振る舞い数 > 最大振る舞いメソッド数)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"This object exposes {状態プロパティ群.Length} trivial public state properties but only {振る舞い数} public behavior method(s). If it is a domain object rather than an explicit data carrier, move behavior that owns this state into the object.",
                    記号.ToDisplayString()
                );
            }
        }
    }

    private static bool 単純な公開状態プロパティである(PropertyDeclarationSyntax プロパティ)
    {
        if (!プロパティ.Modifiers.Any(SyntaxKind.PublicKeyword) || プロパティ.AccessorList is null)
        {
            return false;
        }

        var アクセサ群 = プロパティ.AccessorList.Accessors;
        return アクセサ群.Count >= 1
            && アクセサ群.All(アクセサ => アクセサ.Body is null && アクセサ.ExpressionBody is null);
    }
}
