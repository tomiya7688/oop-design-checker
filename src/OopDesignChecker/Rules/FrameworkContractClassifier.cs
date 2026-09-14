using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class FrameworkContractClassifier
{
    public static bool HasExternalTypeContract(INamedTypeSymbol type)
    {
        if (
            type.BaseType is { SpecialType: not SpecialType.System_Object } baseType
            && !IsSourceDefined(baseType)
        )
        {
            return true;
        }

        return type.AllInterfaces.Any(interfaceType => !IsSourceDefined(interfaceType));
    }

    private static bool IsSourceDefined(INamedTypeSymbol type) =>
        type.Locations.Any(location => location.IsInSource);
}
