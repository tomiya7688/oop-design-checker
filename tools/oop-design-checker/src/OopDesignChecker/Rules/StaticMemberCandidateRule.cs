using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 静的メンバー候補規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP103", "Static member candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var 宣言 in 構文木.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            )
            {
                foreach (var 診断 in クラスを解析する(宣言, 意味モデル))
                {
                    yield return 診断;
                }
            }
        }
    }

    private IEnumerable<DesignDiagnostic> クラスを解析する(
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    )
    {
        if (!解析対象クラスである(宣言, 意味モデル))
        {
            yield break;
        }

        foreach (var メソッド in 宣言.Members.OfType<MethodDeclarationSyntax>())
        {
            var メソッド記号 = 静的化候補メソッドを取得する(メソッド, 宣言, 意味モデル);
            if (メソッド記号 is null)
            {
                continue;
            }

            yield return 診断生成器.生成する(
                規則情報,
                メソッド.Identifier.GetLocation(),
                "This method does not use instance state and can be static.",
                メソッド記号.ToDisplayString()
            );
        }
    }

    private static bool 解析対象クラスである(ClassDeclarationSyntax 宣言, SemanticModel 意味モデル)
    {
        if (
            意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol クラス記号
            || 未解決の基底契約を持つ(宣言, 意味モデル)
            || フレームワーク契約判定器.外部基底型契約を持つ(クラス記号)
            || フレームワーク契約判定器.フレームワーク契約属性を持つ(クラス記号)
        )
        {
            return false;
        }

        return !静的適格性判定器.クラスを静的化できる(宣言, クラス記号, 意味モデル);
    }

    private static IMethodSymbol? 静的化候補メソッドを取得する(
        MethodDeclarationSyntax メソッド,
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    )
    {
        if (
            意味モデル.GetDeclaredSymbol(メソッド) is not IMethodSymbol メソッド記号
            || メソッド記号.IsStatic
            || メソッド記号.IsAbstract
            || メソッド記号.IsVirtual
            || メソッド記号.IsOverride
            || メソッド.ExplicitInterfaceSpecifier is not null
            || フレームワーク契約判定器.フレームワーク契約属性を持つ(メソッド記号)
            || シンボル操作器.インターフェースメンバーを実装している(メソッド記号, 宣言, 意味モデル)
            || インスタンス使用検査器.インスタンス状態を使う(メソッド, メソッド記号, 意味モデル)
        )
        {
            return null;
        }

        return メソッド記号;
    }

    private static bool 未解決の基底契約を持つ(
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    ) =>
        宣言.BaseList?.Types.Any(基底型 =>
            意味モデル.GetTypeInfo(基底型.Type).Type is null or { TypeKind: TypeKind.Error }
        ) == true;
}
