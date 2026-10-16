using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Rules;

internal static class 静的適格性判定器
{
    public static bool クラスを静的化できる(
        ClassDeclarationSyntax 宣言,
        INamedTypeSymbol シンボル,
        SemanticModel 意味モデル
    )
    {
        if (
            シンボル.IsStatic
            || シンボル.IsAbstract
            || シンボル.Interfaces.Length > 0
            || フレームワーク契約判定器.フレームワーク契約属性を持つ(シンボル)
        )
        {
            return false;
        }

        if (シンボル.BaseType is { SpecialType: not SpecialType.System_Object })
        {
            return false;
        }

        if (
            シンボル.InstanceConstructors.Any(コンストラクター =>
                !コンストラクター.IsImplicitlyDeclared && コンストラクター.Parameters.Length > 0
            )
        )
        {
            return false;
        }

        if (
            シンボル.GetMembers()
                .Any(メンバー =>
                    メンバー switch
                    {
                        IFieldSymbol { IsStatic: false } => true,
                        IPropertySymbol { IsStatic: false } => true,
                        IEventSymbol { IsStatic: false } => true,
                        _ => false,
                    }
                )
        )
        {
            return false;
        }

        var インスタンスメソッド群 = 宣言
            .Members.OfType<MethodDeclarationSyntax>()
            .Select(メソッド =>
                (Syntax: メソッド, Symbol: 意味モデル.GetDeclaredSymbol(メソッド) as IMethodSymbol)
            )
            .Where(組 => 組.Symbol is { IsStatic: false })
            .ToArray();

        if (インスタンスメソッド群.Length == 0)
        {
            return シンボル.GetMembers().Any(メンバー => メンバー.IsStatic);
        }

        foreach (var 組 in インスタンスメソッド群)
        {
            var メソッド記号 = 組.Symbol!;
            if (
                メソッド記号.IsAbstract
                || メソッド記号.IsVirtual
                || メソッド記号.IsOverride
                || フレームワーク契約判定器.フレームワーク契約属性を持つ(メソッド記号)
            )
            {
                return false;
            }

            if (
                組.Syntax.ExplicitInterfaceSpecifier is not null
                || インスタンス使用検査器.インスタンス状態を使う(
                    組.Syntax,
                    メソッド記号,
                    意味モデル
                )
            )
            {
                return false;
            }
        }

        return true;
    }
}
