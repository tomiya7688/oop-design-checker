using System.Collections.ObjectModel;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker;

/*
{
  "責務": "1回の解析で得た診断と設定を、呼び出し側の変更から独立して保持します。",
  "フィールド": {"_診断一覧": "コピー後の変更できない診断一覧です。", "_設定": "設定リストをコピーした実行時の設定です。"},
  "処理": ["診断一覧を読み取り専用のコピーとして保存します。"],
  "引数": {"Diagnostics": "解析完了時点の診断です。", "FailureThreshold": "上書き指定を反映した実効失敗基準です。", "ConfigurationPath": "読み込んだ設定のパスです。"},
  "戻り値": "診断と実効失敗基準を保持する解析結果です。"
}
*/
public sealed record CheckerRunResult(
    IReadOnlyList<DesignDiagnostic> Diagnostics,
    DesignDiagnosticSeverity FailureThreshold,
    string? ConfigurationPath
)
{
    private ReadOnlyCollection<DesignDiagnostic> _診断一覧 = 一覧を固定する(Diagnostics);
    private CheckerConfiguration _設定 = new();

    // コンストラクターに加え、初期化子とwith式で差し替える場合も所有権を分離します。
    public IReadOnlyList<DesignDiagnostic> Diagnostics
    {
        get => _診断一覧;
        init => _診断一覧 = 一覧を固定する(value);
    }

    public CheckerConfiguration Configuration
    {
        get => _設定;
        init => _設定 = 設定を固定する(value);
    }

    // with式で失敗基準を変更した場合も、同じ診断一覧と現在の基準から判定します。
    public bool ShouldFail => Diagnostics.Any(診断 => 診断.Severity >= FailureThreshold);

    /*
    {
      "責務": "入力と別の配列を所有し、公開した一覧からの変更も防ぎます。",
      "処理": ["nullを拒否します。", "要素をコピーした配列を読み取り専用のラッパーで保護します。"],
      "引数": {"一覧": "コピーする非nullの一覧です。"},
      "戻り値": "元の一覧やIListへのキャストでは書き換えられないコピーです。"
    }
    */
    private static ReadOnlyCollection<T> 一覧を固定する<T>(IReadOnlyList<T> 一覧)
    {
        ArgumentNullException.ThrowIfNull(一覧);
        return Array.AsReadOnly(一覧.ToArray());
    }

    /*
    {
      "責務": "この結果が使用する設定リストの所有権を分離します。",
      "処理": ["設定モデルのinitへ一覧を渡し、読み取り専用コピーを作ります。", "元の失敗基準と、init専用プロパティだけを持つ規則設定を保持します。"],
      "引数": {"設定": "解析時に使用した非nullの設定です。"},
      "戻り値": "リストの外部変更を受けない設定です。設定モデル全体の妥当性検証は行いません。"
    }
    */
    private static CheckerConfiguration 設定を固定する(CheckerConfiguration 設定)
    {
        ArgumentNullException.ThrowIfNull(設定);
        return new CheckerConfiguration
        {
            IgnoredPaths = 設定.IgnoredPaths,
            DisabledRules = 設定.DisabledRules,
            FailureThreshold = 設定.FailureThreshold,
            RuleSettings = 設定.RuleSettings,
        };
    }
}
