using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class DataCarrierClassifier
{
    public static bool IsExplicitDataCarrier(INamedTypeSymbol type)
    {
        var name = type.Name;
        return HasDataCarrierMarker(type)
            || name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Model", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Request", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Response", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Config", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Message", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Command", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Payload", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Record", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Row", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Document", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDataCarrierMarker(INamedTypeSymbol type) =>
        type.GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass?.Name
                    is "SerializableAttribute"
                        or "DataContractAttribute"
                        or "JsonObjectAttribute"
                        or "MessagePackObjectAttribute"
                        or "ProtoContractAttribute"
            );
}
