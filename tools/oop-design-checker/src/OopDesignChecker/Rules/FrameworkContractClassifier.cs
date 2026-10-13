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

    /*
    {
      責務: symbolが外部定義の属性を持つ事実を返す
      引数: [symbol: 属性を調べる対象]
      戻り値: 外部定義の属性があればtrue
    }
    */
    public static bool HasExternalAttribute(ISymbol symbol) =>
        symbol
            .GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass is INamedTypeSymbol attributeType
                && !IsSourceDefined(attributeType)
            );

    /*
    {
      責務: 外部属性のうち既知のframework契約だけを判定する
      処理: [既知の完全修飾名に一致する外部属性を探す]
      引数: [symbol: 属性を調べる対象]
      戻り値: framework契約属性があればtrue
    }
    */
    public static bool HasFrameworkContractAttribute(ISymbol symbol)
    {
        if (!HasExternalAttribute(symbol))
        {
            return false;
        }

        return symbol
            .GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass is INamedTypeSymbol attributeType
                && !IsSourceDefined(attributeType)
                && IsFrameworkContractAttribute(
                    attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                )
            );
    }

    /*
    {
      責務: 除外の根拠にできるframework属性を分類する
      処理: [callback, discovery, interop, serializationの既知契約名を照合する]
      引数: [fullyQualifiedName: 属性型の完全修飾名]
      戻り値: 判定対象の契約属性ならtrue
    }
    */
    private static bool IsFrameworkContractAttribute(string fullyQualifiedName) =>
        fullyQualifiedName
            is
                // Serializer-invoked callbacks participate in the object lifecycle.
                "global::System.Runtime.Serialization.OnDeserializingAttribute"
                or "global::System.Runtime.Serialization.OnDeserializedAttribute"
                or "global::System.Runtime.Serialization.OnSerializingAttribute"
                or "global::System.Runtime.Serialization.OnSerializedAttribute"
                // The runtime discovers this method for module initialization.
                or "global::System.Runtime.CompilerServices.ModuleInitializerAttribute"
                // These types and methods declare unmanaged interop contracts.
                or "global::System.Runtime.InteropServices.ComImportAttribute"
                or "global::System.Runtime.InteropServices.DllImportAttribute"
                or "global::System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"
                // Serializable types participate in a framework-owned data shape.
                or "global::System.SerializableAttribute";

    private static bool IsSourceDefined(INamedTypeSymbol type) =>
        type.Locations.Any(location => location.IsInSource);
}
