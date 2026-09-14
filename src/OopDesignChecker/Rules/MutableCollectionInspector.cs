using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class MutableCollectionInspector
{
    public static bool IsMutableCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var original = namedType.OriginalDefinition;
        var namespaceName = original.ContainingNamespace.ToDisplayString();
        var metadataName = original.MetadataName;

        return namespaceName switch
        {
            "System.Collections.Generic" => metadataName is
                "List`1" or
                "Dictionary`2" or
                "HashSet`1" or
                "ICollection`1" or
                "IList`1" or
                "IDictionary`2" or
                "ISet`1",
            "System.Collections" => metadataName is
                "ICollection" or
                "IList" or
                "IDictionary",
            _ => false
        };
    }
}
