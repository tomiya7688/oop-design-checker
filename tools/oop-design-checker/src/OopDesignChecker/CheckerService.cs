using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker;

/*
{
  "責務": "既存の公開解析APIを維持し、既定の依存構成へ呼び出しを委譲します。",
  "フィールド": []
}
*/
public static class CheckerService
{
    /*
    {
      "責務": "既定の構成で1回の解析を実行します。",
      "処理": ["既定の依存を組み立てます。", "入力を変更せず解析ユースケースへ渡します。"],
      "引数": {"targetPath": "解析対象です。", "configurationPath": "明示指定する設定パスです。", "failureThresholdOverride": "実効失敗基準の上書き値です。", "cancellationToken": "キャンセル通知です。"},
      "戻り値": "診断と使用した設定を保持する解析結果です。"
    }
    */
    public static CheckerRunResult Analyze(
        string targetPath,
        string? configurationPath = null,
        DesignDiagnosticSeverity? failureThresholdOverride = null,
        CancellationToken cancellationToken = default
    ) =>
        既定の解析構成
            .作る()
            .実行する(targetPath, configurationPath, failureThresholdOverride, cancellationToken);
}
