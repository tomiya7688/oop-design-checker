using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class プロジェクト抽象判定器
{
    public static INamedTypeSymbol? 意味のある抽象候補を探す(INamedTypeSymbol 具象型) =>
        具象型.AllInterfaces.FirstOrDefault(意味のある抽象である);

    /*
    {
      責務: OOP305が利用契約を比較する意味のあるインターフェース候補を列挙する
      処理: 具象型の全インターフェースから候補を選び、完全修飾名の昇順で返す
      引数: [
        具象型: 抽象候補を調べる具象型
      ]
      戻り値: OOP305で利用契約を検証できる決定的な順序のインターフェース候補
    }
    */
    public static IReadOnlyList<INamedTypeSymbol> 意味のある抽象候補群を探す(
        INamedTypeSymbol 具象型
    ) =>
        具象型
            .AllInterfaces.Where(意味のある抽象である)
            .OrderBy(
                インターフェース型 =>
                    インターフェース型.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                StringComparer.Ordinal
            )
            .ToArray();

    public static bool 意味のある抽象である(INamedTypeSymbol インターフェース型) =>
        インターフェース型.TypeKind == TypeKind.Interface
        && インターフェース型.Locations.Any(location => location.IsInSource)
        && 振る舞い契約を持つ(インターフェース型);

    private static bool 振る舞い契約を持つ(INamedTypeSymbol インターフェース型) =>
        インターフェース型.GetMembers().Any(振る舞いメンバーである)
        || インターフェース型.AllInterfaces.Any(parent =>
            parent.GetMembers().Any(振る舞いメンバーである)
        );

    private static bool 振る舞いメンバーである(ISymbol メンバー) =>
        アクセス可能なインスタンスメンバーである(メンバー)
        && メンバー
            is IMethodSymbol { MethodKind: MethodKind.Ordinary }
                or IPropertySymbol
                or IEventSymbol;

    /*
    {
      責務: 同じプロジェクトの利用側がインスタンス経由で使えるインターフェースメンバーを選ぶ
      処理: 静的メンバーを除外し、publicまたは同じアセンブリから使えるメンバーだけを許可する
      引数: [
        メンバー: 利用可能性を調べるインターフェースメンバー
      ]
      戻り値: OOP305とOOP306のインスタンス契約として利用できる場合はtrue
      補足: 抽象候補はソース定義のインターフェースに限るため、internalとprotected internalは同じプロジェクト内から使える
    }
    */
    private static bool アクセス可能なインスタンスメンバーである(ISymbol メンバー) =>
        !メンバー.IsStatic
        && メンバー.DeclaredAccessibility
            is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal;
}
