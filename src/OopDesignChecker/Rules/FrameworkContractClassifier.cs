using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class FrameworkContractClassifier
{
    public static bool HasExternalTypeContract(INamedTypeSymbol type) =>
        HasExternalBaseTypeContract(type)
        || type.AllInterfaces.Any(interfaceType => !IsSourceDefined(interfaceType));

    public static bool HasExternalBaseTypeContract(INamedTypeSymbol type) =>
        type.BaseType is { SpecialType: not SpecialType.System_Object } baseType
        && !IsSourceDefined(baseType);

    public static bool HasExternalFrameworkAttribute(ISymbol symbol) =>
        symbol
            .GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass is INamedTypeSymbol attributeType
                && !IsSourceDefined(attributeType)
            );

    private static bool IsSourceDefined(INamedTypeSymbol type) =>
        type.Locations.Any(location => location.IsInSource);
}
