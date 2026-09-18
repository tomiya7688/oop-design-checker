using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class DataCarrierClassifier
{
    private static readonly string[] DataCarrierNameSuffixes =
    [
        "Dto",
        "Model",
        "Request",
        "Response",
        "Options",
        "Configuration",
        "Config",
        "Message",
        "Event",
        "Command",
        "Payload",
        "Record",
        "Row",
        "Document",
        "ViewModel",
    ];

    private static readonly string[] KnownDataCarrierAttributes =
    [
        "System.SerializableAttribute",
        "System.Runtime.Serialization.DataContractAttribute",
        "System.Xml.Serialization.XmlRootAttribute",
        "System.Xml.Serialization.XmlTypeAttribute",
        "Newtonsoft.Json.JsonObjectAttribute",
        "MessagePack.MessagePackObjectAttribute",
        "ProtoBuf.ProtoContractAttribute",
    ];

    public static bool IsExplicitDataCarrier(INamedTypeSymbol type) =>
        HasDataCarrierMarker(type) || (HasDataCarrierName(type) && HasDataCarrierShape(type));

    private static bool HasDataCarrierName(INamedTypeSymbol type) =>
        DataCarrierNameSuffixes.Any(suffix =>
            type.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        );

    private static bool HasDataCarrierShape(INamedTypeSymbol type)
    {
        var instanceMembers = type.GetMembers().Where(member => !member.IsStatic).ToArray();
        var hasPublicState = instanceMembers.Any(member =>
            member
                is IPropertySymbol
                    {
                        DeclaredAccessibility: Accessibility.Public,
                        IsIndexer: false,
                    }
                or IFieldSymbol { DeclaredAccessibility: Accessibility.Public }
        );
        if (!hasPublicState)
        {
            return false;
        }

        return !instanceMembers.OfType<IMethodSymbol>().Any(method =>
            method.MethodKind == MethodKind.Ordinary
            && method.DeclaredAccessibility == Accessibility.Public
        );
    }

    private static bool HasDataCarrierMarker(INamedTypeSymbol type) =>
        type.GetAttributes().Any(attribute =>
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            return attributeName is not null
                && KnownDataCarrierAttributes.Contains(attributeName, StringComparer.Ordinal);
        });
}
