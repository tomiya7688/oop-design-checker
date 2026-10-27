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
      "処理": ["対象の存在を確認します。", "対象を絶対パスへ変換し、既存の設定読み込み器へ渡します。", "設定エラーを構造化された失敗へ変換します。"],
      "引数": {"対象パス": "利用者が指定した対象です。", "設定パス": "明示指定する設定パスです。"},
      "戻り値": "絶対パスと使用する設定をまとめた入力です。"
    }
    */
    public static 解析入力 読み込む(string 対象パス, string? 設定パス)
    {
        // 設定を探す前に対象の存在を確認する、従来のエラー優先順を維持します。
        if (!Directory.Exists(対象パス) && !File.Exists(対象パス))
        {
            throw new AnalysisFailureException(
                AnalysisFailureKind.Target,
                $"解析する対象が見つかりません: {対象パス}\n対象欄のパスを確認してください。",
                $"Target does not exist: {対象パス}",
                対象パス
            );
        }

        var 対象の絶対パス = Path.GetFullPath(対象パス);
        try
        {
            var 読込済み設定 = チェッカー設定読み込み器.Load(対象の絶対パス, 設定パス);
            return new 解析入力(対象の絶対パス, 読込済み設定);
        }
        catch (AnalysisFailureException)
        {
            throw;
        }
        catch (Exception 例外)
            when (例外
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            var 問題の設定パス = (例外 as 設定ファイル読込例外)?.設定ファイルパス ?? 設定パス;
            var 日本語説明 = string.IsNullOrWhiteSpace(問題の設定パス)
                ? "設定ファイルを読み込めませんでした。JSONの形式や設定値を確認してください。"
                : $"設定ファイルを読み込めませんでした: {問題の設定パス}\n設定欄のパスとJSONの内容を確認してください。";
            throw new AnalysisFailureException(
                AnalysisFailureKind.Configuration,
                日本語説明,
                例外.Message,
                string.IsNullOrWhiteSpace(問題の設定パス) ? null : 問題の設定パス,
                例外
            );
        }
    }
}

/*
{
  "責務": "外部入力の読込結果を解析ユースケースへ渡します。",
  "フィールド": {"対象の絶対パス": "ローダーへ渡す解決済みパスです。", "読込済み設定": "設定モデルと使用した設定ファイルのパスです。"}
}
*/
internal sealed record 解析入力(string 対象の絶対パス, 読込済みチェッカー設定 読込済み設定);
