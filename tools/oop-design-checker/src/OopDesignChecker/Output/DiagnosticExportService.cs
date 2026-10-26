using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "DiagnosticExportService": "GUIなどから診断をJSONまたはSARIFファイルへ保存します。"
  }
}
*/
public static class DiagnosticExportService
{
    /*
    {
      "責務": {
        "Export": "GUIなどから診断をJSONまたはSARIFファイルへ保存します。"
      },
      "処理": [
            "診断一覧と出力パスを検証します。",
            "保存形式に対応する書き込み器でファイルを出力します。"
      ],
      "引数": {
        "diagnostics": "保存する診断一覧です。",
        "outputPath": "保存するファイルパスです。",
        "format": "JSONまたはSARIFの保存形式です。"
      },
      "戻り値": []
    }
    */
    public static void Export(
        IReadOnlyList<DesignDiagnostic> diagnostics,
        string outputPath,
        DiagnosticExportFormat format
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        // 保存専用の形式契約を使い、CLIの形式やオプションには依存しません。
        var 書き込み器 = 診断書き込み器生成器.ファイル保存用を作る(format, outputPath);
        書き込み器.Write(diagnostics);
    }
}
