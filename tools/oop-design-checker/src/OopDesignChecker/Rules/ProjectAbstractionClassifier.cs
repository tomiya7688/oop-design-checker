using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class ProjectAbstractionClassifier
{
    public static INamedTypeSymbol? FindMeaningfulAbstraction(INamedTypeSymbol concreteType) =>
        concreteType.AllInterfaces.FirstOrDefault(IsMeaningfulAbstraction);

    /*
    {
      責務: OOP305が利用契約を比較する意味のあるinterface候補を列挙する
      処理: 具象型の全interfaceから意味のある候補を選び、完全修飾名の昇順で返す
      引数: [
        concreteType: 抽象候補を調べる具象型
      ]
      戻り値: OOP305で利用契約を検証できる決定的な順序のinterface候補
    }
    */
    public static IReadOnlyList<INamedTypeSymbol> FindMeaningfulAbstractions(
        INamedTypeSymbol concreteType
    ) =>
        concreteType
            .AllInterfaces.Where(IsMeaningfulAbstraction)
            .OrderBy(
                interfaceType =>
                    interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                StringComparer.Ordinal
            )
            .ToArray();

    public static bool IsMeaningfulAbstraction(INamedTypeSymbol interfaceType) =>
        interfaceType.TypeKind == TypeKind.Interface
        && interfaceType.Locations.Any(location => location.IsInSource)
        && HasBehavioralContract(interfaceType);

    private static bool HasBehavioralContract(INamedTypeSymbol interfaceType) =>
        interfaceType.GetMembers().Any(IsBehavioralMember)
        || interfaceType.AllInterfaces.Any(parent => parent.GetMembers().Any(IsBehavioralMember));

    private static bool IsBehavioralMember(ISymbol member) =>
        IsAccessibleInstanceMember(member)
        && member
            is IMethodSymbol { MethodKind: MethodKind.Ordinary }
                or IPropertySymbol
                or IEventSymbol;

    /*
    {
      責務: 同じproject内のconsumerがinstance経由で使えるinterface memberを選ぶ
      処理: static memberを除外し、publicまたは同じassemblyから使えるmemberだけを許可する
      引数: [
        member: 利用可能性を調べるinterface member
      ]
      戻り値: OOP305とOOP306のinstance契約として利用できる場合はtrue
      補足: 抽象候補はsource interfaceに限るため、internalとprotected internalは同じproject内から使える
    }
    */
    private static bool IsAccessibleInstanceMember(ISymbol member) =>
        !member.IsStatic
        && member.DeclaredAccessibility
            is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal;
}
