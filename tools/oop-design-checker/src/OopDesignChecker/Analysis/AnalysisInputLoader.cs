using OopDesignChecker.Configuration;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": "外部入力の存在確認と設定読込を行い、解析用の入力へまとめます。",
  "フィールド": []
}
*/
internal static class 解析入力読み込み器
{
    /*
    {
      "責務": "解析対象と設定をファイルシステムから読み込みます。",
      "処理": ["対象の存在を確認します。", "対象を絶対パスへ変換し、既存の設定読み込み器へ渡します。"],
      "引数": {"対象パス": "利用者が指定した対象です。", "設定パス": "明示指定する設定パスです。"},
      "戻り値": "絶対パスと使用する設定をまとめた入力です。"
    }
    */
    public static 解析入力 読み込む(string 対象パス, string? 設定パス)
    {
        // 設定を探す前に対象の存在を確認する、従来のエラー優先順を維持します。
        if (!Directory.Exists(対象パス) && !File.Exists(対象パス))
            throw new InvalidOperationException($"Target does not exist: {対象パス}");

        var 対象の絶対パス = Path.GetFullPath(対象パス);
        var 読込済み設定 = チェッカー設定読み込み器.Load(対象の絶対パス, 設定パス);
        return new 解析入力(対象の絶対パス, 読込済み設定);
    }
}

/*
{
  "責務": "外部入力の読込結果を解析ユースケースへ渡します。",
  "フィールド": {"対象の絶対パス": "ローダーへ渡す解決済みパスです。", "読込済み設定": "設定モデルと使用した設定ファイルのパスです。"}
}
*/
internal sealed record 解析入力(string 対象の絶対パス, 読込済みチェッカー設定 読込済み設定);
