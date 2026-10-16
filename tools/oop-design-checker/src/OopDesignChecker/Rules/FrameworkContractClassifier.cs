using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Rules;

internal static class フレームワーク契約判定器
{
    public static bool 外部型契約を持つ(INamedTypeSymbol 型) =>
        外部基底型契約を持つ(型)
        || 型.AllInterfaces.Any(インターフェース型 => !ソース定義である(インターフェース型));

    public static bool 外部基底型契約を持つ(INamedTypeSymbol 型) =>
        型.BaseType is { SpecialType: not SpecialType.System_Object } 基底型
        && !ソース定義である(基底型);

    /*
    {
      責務: シンボルが外部定義の属性を持つか判定する
      引数: [シンボル: 属性を調べる対象]
      戻り値: 外部定義の属性を持つ場合はtrue
    }
    */
    public static bool 外部属性を持つ(ISymbol シンボル) =>
        シンボル.GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass is INamedTypeSymbol attributeType
                && !ソース定義である(attributeType)
            );

    /*
    {
      責務: 外部属性のうち既知のフレームワーク契約だけを判定する
      処理: 既知の完全修飾名に一致する外部属性を探す
      引数: [シンボル: 属性を調べる対象]
      戻り値: フレームワーク契約属性を持つ場合はtrue
    }
    */
    public static bool フレームワーク契約属性を持つ(ISymbol シンボル)
    {
        if (!外部属性を持つ(シンボル))
        {
            return false;
        }

        return シンボル.GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass is INamedTypeSymbol attributeType
                && !ソース定義である(attributeType)
                && フレームワーク契約属性である(
                    attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                )
            );
    }

    /*
    {
      責務: 除外の根拠にできるフレームワーク属性を分類する
      処理: コールバック、自動検出、相互運用、シリアライズに関する既知の契約名を照合する
      引数: [完全修飾名: 属性型の完全修飾名]
      戻り値: 判定対象の契約属性である場合はtrue
    }
    */
    private static bool フレームワーク契約属性である(string 完全修飾名) =>
        完全修飾名
            is
                // シリアライズ処理のコールバックは、オブジェクトのライフサイクルに参加します。
                "global::System.Runtime.Serialization.OnDeserializingAttribute"
                or "global::System.Runtime.Serialization.OnDeserializedAttribute"
                or "global::System.Runtime.Serialization.OnSerializingAttribute"
                or "global::System.Runtime.Serialization.OnSerializedAttribute"
                // ランタイムはモジュール初期化メソッドを検出します。
                or "global::System.Runtime.CompilerServices.ModuleInitializerAttribute"
                // これらの型とメソッドは、アンマネージ相互運用の契約を宣言します。
                or "global::System.Runtime.InteropServices.ComImportAttribute"
                or "global::System.Runtime.InteropServices.DllImportAttribute"
                or "global::System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"
                // シリアライズ可能な型は、フレームワーク管理のデータ形式に参加します。
                or "global::System.SerializableAttribute";

    private static bool ソース定義である(INamedTypeSymbol 型) =>
        型.Locations.Any(location => location.IsInSource);
}
