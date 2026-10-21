using System.Text.Json;
using System.Text.Json.Serialization;

namespace OopDesignChecker.Configuration;

/*
{
  "責務": "設定JSONと解析用設定の形式を相互変換し、JSON形式エラーを説明します。",
  "フィールド": {"シリアライザー設定": "既存のJSONキーと入力許容形式を保つ変換設定です。"}
}
*/
public static class CheckerConfigurationJson
{
    private static readonly JsonSerializerOptions シリアライザー設定 = シリアライザー設定を作る();

    /*
    {
      "責務": "JSONを読み終えた最終値から解析用設定を生成します。",
      "処理": ["JSON入力用の型へ読み込みます。", "設定モデルへ変換して値を検証します。", "JSON形式エラーには利用者向けの説明を付けます。"],
      "引数": {"json": "読み込む設定JSONです。"},
      "戻り値": "構築時に検証済みの解析用設定です。"
    }
    */
    public static CheckerConfiguration Parse(string json)
    {
        try
        {
            var 設定入力 =
                JsonSerializer.Deserialize<チェッカー設定JSON入力>(json, シリアライザー設定)
                ?? throw new InvalidOperationException("The checker configuration is empty.");
            return 設定入力.解析用設定を作る();
        }
        catch (JsonException 例外)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration JSON: {例外.Message}",
                例外
            );
        }
    }

    /*
    {
      "責務": "検証済みの設定を既存のJSON形式へ変換します。",
      "処理": ["null引数を拒否します。", "設定モデルをJSONへ変換します。"],
      "引数": {"configuration": "構築時に検証された設定です。"},
      "戻り値": "従来のキーと列挙値表記を使うJSONです。"
    }
    */
    public static string Serialize(CheckerConfiguration configuration)
    {
        Validate(configuration);
        return JsonSerializer.Serialize(configuration, シリアライザー設定);
    }

    /*
    {
      "責務": "既存の公開検証APIを保ち、null引数を拒否します。",
      "処理": ["不変条件はモデルのinitが保証するため再定義せず、nullだけを拒否します。"],
      "引数": {"configuration": "構築時に検証済みの設定です。"},
      "戻り値": []
    }
    */
    public static void Validate(CheckerConfiguration configuration) =>
        ArgumentNullException.ThrowIfNull(configuration);

    /*
    {
      "責務": "設定JSONの既存の入力許容形式と出力形式を定義します。",
      "処理": ["キー、コメント、カンマの扱いを設定します。", "整数を拒否する列挙値変換を追加します。"],
      "引数": [],
      "戻り値": "JSON変換で共有する設定です。"
    }
    */
    private static JsonSerializerOptions シリアライザー設定を作る()
    {
        var 設定 = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };
        設定.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        );
        return 設定;
    }
}
