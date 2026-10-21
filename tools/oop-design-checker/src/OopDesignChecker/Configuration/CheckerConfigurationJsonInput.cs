using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

/*
{
  "責務": "JSONの最終値を一時的に保持し、検証を所有する解析用設定へ変換します。",
  "フィールド": {"IgnoredPaths": "nullを含み得る入力値です。", "DisabledRules": "nullを含み得る入力値です。", "FailureThreshold": "JSONの重大度です。", "RuleSettings": "nullを含み得る規則設定入力です。"}
}
*/
internal sealed class チェッカー設定JSON入力
{
    public IReadOnlyList<string>? IgnoredPaths { get; init; } = [];
    public IReadOnlyList<string>? DisabledRules { get; init; } = [];
    public DesignDiagnosticSeverity FailureThreshold { get; init; } =
        CheckerConfiguration.既定の失敗基準;
    public 規則設定JSON入力? RuleSettings { get; init; } = new();

    /*
    {
      "責務": "JSONの最終値から、不変条件を満たす解析用設定を生成します。",
      "処理": ["既存の検証順でモデルのinitへ値を渡します。", "一覧のコピーと値の検証はモデルへ委譲します。"],
      "引数": [],
      "戻り値": "不正値を保持しない解析用設定です。"
    }
    */
    public CheckerConfiguration 解析用設定を作る() =>
        new()
        {
            // 重複キーを読み終えてから検証し、入力キー順によらず既存のエラー説明順を保ちます。
            FailureThreshold = FailureThreshold,
            IgnoredPaths = IgnoredPaths!,
            DisabledRules = DisabledRules!,
            RuleSettings = RuleSettings?.解析用設定を作る()!,
        };
}

/*
{
  "責務": "規則設定のJSON入力を一時的に保持します。",
  "フィールド": {"Oop304": "nullを含み得る継承深度設定の入力です。"}
}
*/
internal sealed class 規則設定JSON入力
{
    public 継承深度設定JSON入力? Oop304 { get; init; } = new();

    /*
    {
      "責務": "規則設定の入力値を検証するモデルへ引き渡します。",
      "処理": ["OOP304の最終入力を変換し、nullの拒否はモデルへ委譲します。"],
      "引数": [],
      "戻り値": "検証済みの規則設定です。"
    }
    */
    public CheckerRuleSettings 解析用設定を作る() => new() { Oop304 = Oop304?.解析用設定を作る()! };
}

/*
{
  "責務": "継承深度設定のJSON入力を一時的に保持します。",
  "フィールド": {"WarningDepth": "まだ検証していない入力深度です。"}
}
*/
internal sealed class 継承深度設定JSON入力
{
    public int WarningDepth { get; init; } = InheritanceDepthRuleSettings.既定の警告深度;

    /*
    {
      "責務": "警告深度の検証を設定モデルへ委譲します。",
      "処理": ["最終入力値をモデルのinitへ渡します。"],
      "引数": [],
      "戻り値": "1以上の警告深度を持つ設定です。"
    }
    */
    public InheritanceDepthRuleSettings 解析用設定を作る() => new() { WarningDepth = WarningDepth };
}
