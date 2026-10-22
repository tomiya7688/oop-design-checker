using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "診断書き込み器生成器": "形式に応じた書き込み器を生成し、text専用の設定は専用の生成経路で受け取ります。"
  }
}
*/
internal static class 診断書き込み器生成器
{
    /*
    {
      "責務": {
        "Create": "指定形式の書き込み器を既定の表示設定で生成します。"
      },
      "処理": [
        "形式に応じた書き込み器を選びます。"
      ],
      "引数": {
        "format": "出力形式です。",
        "outputPath": "未指定時は標準出力です。GitHub注釈は常に標準出力です。"
      },
      "戻り値": "指定形式の書き込み器です。"
    }
    */
    public static 診断書き込み器 Create(診断出力形式 format, string? outputPath) =>
        format switch
        {
            診断出力形式.Text => テキストを作る(outputPath),
            診断出力形式.Json => new JSON診断書き込み器(outputPath),
            診断出力形式.Sarif => new SARIF診断書き込み器(outputPath),
            診断出力形式.GitHub => new GitHub注釈診断書き込み器(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

    /*
    {
      "責務": {"テキストを作る": "text専用の言語と詳細表示を保持する書き込み器を生成します。"},
      "処理": ["text専用の表示設定をコンソール診断書き込み器に渡します。"],
      "引数": {"出力パス": "未指定時は標準出力です。", "表示言語": "診断を表示する言語です。", "詳細表示": "規則の見出しも出力する場合にtrueです。"},
      "戻り値": "表示設定を保持した診断書き込み器です。"
    }
    */
    public static 診断書き込み器 テキストを作る(
        string? 出力パス,
        UserInterfaceLanguage 表示言語 = UserInterfaceLanguage.Japanese,
        bool 詳細表示 = false
    ) => new コンソール診断書き込み器(出力パス, 表示言語, 詳細表示);

    /*
    {
      "責務": {"ファイル保存用を作る": "保存可能なJSONまたはSARIFのwriterを生成し、未定義の保存形式を拒否します。"},
      "処理": ["公開の保存形式を共有出力形式へ対応付けます。", "指定したファイルを出力先とするwriterを生成します。"],
      "引数": {"format": "JSONまたはSARIFの保存形式です。", "outputPath": "呼出側で検証した保存先ファイルパスです。"},
      "戻り値": "指定した保存形式と出力先を持つwriterです。",
      "エラー": ["未定義の保存形式ではArgumentOutOfRangeExceptionを送出します。"]
    }
    */
    public static 診断書き込み器 ファイル保存用を作る(
        DiagnosticExportFormat format,
        string outputPath
    ) =>
        Create(
            format switch
            {
                DiagnosticExportFormat.Json => 診断出力形式.Json,
                DiagnosticExportFormat.Sarif => 診断出力形式.Sarif,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            },
            outputPath
        );
}
