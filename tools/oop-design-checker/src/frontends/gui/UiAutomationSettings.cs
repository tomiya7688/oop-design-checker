using System.Globalization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Gui;

/*
{
  "責務": {
    "UI自動化設定": "明示的に有効化されたGUI自動テストの遅延と出力先を読み取ります。"
  },
  "フィールド": {
    "出力先環境変数名": "自動テスト用出力先を指定する環境変数名です。",
    "解析遅延環境変数名": "自動テスト用の遅延を指定する環境変数名です。",
    "最大解析遅延ミリ秒": "誤設定による長い待機を防ぐ30秒上限です。",
    "既定解析遅延ミリ秒": "未設定と不正値で採用する遅延なしの値です。"
  }
}
*/
internal static class UI自動化設定
{
    private const string 出力先環境変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR";
    private const string 解析遅延環境変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";

    // キャンセル操作を自動テストで間に合わせつつ、誤設定で30秒以上待たせない上限です。
    private const int 最大解析遅延ミリ秒 = 30_000;

    // 通常起動と無効なテスト設定は、意図しない待機を追加せず即時実行します。
    private const int 既定解析遅延ミリ秒 = 0;

    internal static int 解析遅延ミリ秒 =>
        解析遅延を読み取る(Environment.GetEnvironmentVariable(解析遅延環境変数名));

    /*
    {
      "責務": {
        "解析遅延を読み取る": "環境変数の整数値をミリ秒として読み、未設定、不正値、範囲外は遅延なしにします。"
      },
      "処理": [
        "環境変数の整数値をミリ秒として読み、未設定、不正値、範囲外は遅延なしにします。"
      ],
      "引数": {
        "設定値": "未設定または0から30000までの整数文字列です。前後の空白を許可します。"
      },
      "戻り値": "有効値または既定値0のミリ秒数です。"
    }
    */
    internal static int 解析遅延を読み取る(string? 設定値) =>
        int.TryParse(設定値, NumberStyles.Integer, CultureInfo.InvariantCulture, out var 遅延)
        && 遅延 is >= 0 and <= 最大解析遅延ミリ秒
            ? 遅延
            : 既定解析遅延ミリ秒;

    internal static string? 出力先パス(DiagnosticExportFormat format)
    {
        var directory = Environment.GetEnvironmentVariable(出力先環境変数名);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var fileName =
            format == DiagnosticExportFormat.Sarif
                ? "actual-diagnostics.sarif"
                : "actual-diagnostics.json";
        return Path.Combine(directory, fileName);
    }
}
