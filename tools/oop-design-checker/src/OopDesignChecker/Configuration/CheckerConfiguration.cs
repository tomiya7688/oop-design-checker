using System.Collections.ObjectModel;
using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

/*
{
  "責務": "解析に使用できる設定だけを保持し、入力リストから所有権を分離します。",
  "フィールド": {"_無視対象パス群": "所有する読み取り専用パス一覧です。", "_無効規則識別子群": "所有する読み取り専用規則一覧です。", "_失敗基準": "定義済みの重大度です。", "_規則設定": "非nullの規則設定です。", "既定の失敗基準": "C#とJSONで共有する既定値です。"},
  "エラー": "initで不正値を指定すると、既存の設定キーを含むInvalidOperationExceptionを送出します。"
}
*/
public sealed class CheckerConfiguration
{
    internal const DesignDiagnosticSeverity 既定の失敗基準 = DesignDiagnosticSeverity.Danger;
    private ReadOnlyCollection<string> _無視対象パス群 = Array.AsReadOnly(Array.Empty<string>());
    private ReadOnlyCollection<string> _無効規則識別子群 = Array.AsReadOnly(Array.Empty<string>());
    private DesignDiagnosticSeverity _失敗基準 = 既定の失敗基準;
    private CheckerRuleSettings _規則設定 = new();

    // 公開型とinitによる構築方法を維持し、保存前に必ずコピーします。
    public IReadOnlyList<string> IgnoredPaths
    {
        get => _無視対象パス群;
        init => _無視対象パス群 = 設定一覧のコピーを作る(value, "ignoredPaths");
    }

    public IReadOnlyList<string> DisabledRules
    {
        get => _無効規則識別子群;
        init => _無効規則識別子群 = 設定一覧のコピーを作る(value, "disabledRules");
    }

    public DesignDiagnosticSeverity FailureThreshold
    {
        get => _失敗基準;
        init
        {
            // JSONアダプターの利用有無にかかわらず、不正な重大度を保存しません。
            if (!Enum.IsDefined(value))
                throw new InvalidOperationException("failureThreshold must be a defined severity.");
            _失敗基準 = value;
        }
    }

    public CheckerRuleSettings RuleSettings
    {
        get => _規則設定;
        init =>
            _規則設定 =
                value ?? throw new InvalidOperationException("ruleSettings must be an object.");
    }

    /*
    {
      "責務": "設定リストの非null条件と所有権を保証します。",
      "処理": ["nullなら設定キーを含む説明で拒否します。", "配列へコピーして読み取り専用にします。"],
      "引数": {"一覧": "入力元が所有する一覧です。", "設定キー": "利用者に説明する設定項目です。"},
      "戻り値": "入力元から独立した変更できない一覧です。"
    }
    */
    private static ReadOnlyCollection<string> 設定一覧のコピーを作る(
        IReadOnlyList<string> 一覧,
        string 設定キー
    )
    {
        if (一覧 is null)
            throw new InvalidOperationException($"{設定キー} must be an array.");
        return Array.AsReadOnly(一覧.ToArray());
    }
}

/*
{
  "責務": "解析規則ごとの非nullの設定を保持します。",
  "フィールド": {"_継承深度設定": "OOP304が使用する検証済み設定です。"}
}
*/
public sealed class CheckerRuleSettings
{
    private InheritanceDepthRuleSettings _継承深度設定 = new();

    public InheritanceDepthRuleSettings Oop304
    {
        get => _継承深度設定;
        init =>
            _継承深度設定 =
                value
                ?? throw new InvalidOperationException("ruleSettings.oop304 must be an object.");
    }
}

/*
{
  "責務": "1以上の警告深度を保持し、継承深度規則へ有効な設定を渡します。",
  "フィールド": {"_警告深度": "1以上の警告深度です。", "既定の警告深度": "C#とJSONで共有する既定値です。"}
}
*/
public sealed class InheritanceDepthRuleSettings
{
    internal const int 既定の警告深度 = 4;
    private int _警告深度 = 既定の警告深度;

    public int WarningDepth
    {
        get => _警告深度;
        init
        {
            // 規則を作る前に、単独の設定オブジェクト自身が不変条件を守ります。
            if (value < 1)
                throw new InvalidOperationException(
                    "ruleSettings.oop304.warningDepth must be at least 1."
                );
            _警告深度 = value;
        }
    }
}
