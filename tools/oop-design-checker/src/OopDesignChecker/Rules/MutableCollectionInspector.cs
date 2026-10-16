using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class 可変コレクション検査器
{
    public static bool 可変コレクションである(ITypeSymbol 型)
    {
        if (型 is IArrayTypeSymbol)
        {
            return true;
        }

        if (型 is not INamedTypeSymbol 名前付き型)
        {
            return false;
        }

        var 元の型 = 名前付き型.OriginalDefinition;
        var 名前空間名 = 元の型.ContainingNamespace.ToDisplayString();
        var メタデータ名 = 元の型.MetadataName;

        return 名前空間名 switch
        {
            "System.Collections.Generic" => メタデータ名
                is "List`1"
                    or "Dictionary`2"
                    or "HashSet`1"
                    or "ICollection`1"
                    or "IList`1"
                    or "IDictionary`2"
                    or "ISet`1",
            "System.Collections" => メタデータ名 is "ICollection" or "IList" or "IDictionary",
            _ => false,
        };
    }
}
