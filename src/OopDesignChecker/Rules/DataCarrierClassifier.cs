using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class DataCarrierClassifier
{
    public static bool IsExplicitDataCarrier(INamedTypeSymbol type) =>
        HasDataCarrierMarker(type) || (HasDataCarrierName(type) && HasDataCarrierShape(type));

    private static bool HasDataCarrierName(INamedTypeSymbol type) =>
        type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Model", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Request", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Response", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Message", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Command", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Payload", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Record", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Row", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("Document", StringComparison.OrdinalIgnoreCase)
        || type.Name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase);

    private static bool HasDataCarrierShape(INamedTypeSymbol type)
    {
        var instanceMembers = type.GetMembers().Where(member => !member.IsStatic).ToArray();
        var hasPublicState = instanceMembers.Any(member =>
            member
                is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsIndexer: false }
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
        type.GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass?.ToDisplayString()
                    is "System.SerializableAttribute"
                        or "System.Runtime.Serialization.DataContractAttribute"
                        or "System.Xml.Serialization.XmlRootAttribute"
                        or "System.Xml.Serialization.XmlTypeAttribute"
                        or "Newtonsoft.Json.JsonObjectAttribute"
                        or "MessagePack.MessagePackObjectAttribute"
                        or "ProtoBuf.ProtoContractAttribute"
            );
}
