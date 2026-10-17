using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class データ保持型判定器
{
    public static bool 明示的データ保持型である(INamedTypeSymbol 型) =>
        データ保持属性がある(型) || (データ保持型の命名である(型) && データ保持型の構造である(型));

    private static bool データ保持型の命名である(INamedTypeSymbol 型) =>
        型.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Model", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Request", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Response", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Message", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Command", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Payload", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Record", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Row", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("Document", StringComparison.OrdinalIgnoreCase)
        || 型.Name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase);

    private static bool データ保持型の構造である(INamedTypeSymbol 型)
    {
        var インスタンスメンバー群 = 型.GetMembers()
            .Where(メンバー => !メンバー.IsStatic)
            .ToArray();
        var 公開状態がある = インスタンスメンバー群.Any(メンバー =>
            メンバー
                is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsIndexer: false }
                    or IFieldSymbol { DeclaredAccessibility: Accessibility.Public }
        );
        if (!公開状態がある)
        {
            return false;
        }

        return !インスタンスメンバー群
            .OfType<IMethodSymbol>()
            .Any(メソッド =>
                メソッド.MethodKind == MethodKind.Ordinary
                && メソッド.DeclaredAccessibility == Accessibility.Public
            );
    }

    /*
    {
      "責務": "命名からの推測を含めず、既知の属性によるデータ契約を確認します。",
      "処理": ["宣言属性を既知の完全修飾名と照合します。"],
      "引数": {"型": "データ契約を確認する型です。"},
      "戻り値": "シリアル化などのデータ保持属性がある場合にtrueです。"
    }
    */
    public static bool データ保持属性がある(INamedTypeSymbol 型) =>
        型.GetAttributes()
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
