using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class ProjectAbstractionClassifier
{
    public static INamedTypeSymbol? FindMeaningfulAbstraction(INamedTypeSymbol concreteType) =>
        concreteType.AllInterfaces.FirstOrDefault(IsMeaningfulAbstraction);

    public static bool IsMeaningfulAbstraction(INamedTypeSymbol interfaceType) =>
        interfaceType.TypeKind == TypeKind.Interface
        && interfaceType.Locations.Any(location => location.IsInSource)
        && HasBehavioralContract(interfaceType);

    private static bool HasBehavioralContract(INamedTypeSymbol interfaceType) =>
        interfaceType.GetMembers().Any(IsBehavioralMember)
        || interfaceType.AllInterfaces.Any(parent => parent.GetMembers().Any(IsBehavioralMember));

    private static bool IsBehavioralMember(ISymbol member) =>
        member
            is IMethodSymbol { MethodKind: MethodKind.Ordinary }
                or IPropertySymbol
                or IEventSymbol;
}
