using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal static class 規則一覧
{
    public static IReadOnlyList<I解析規則> 既定の規則一覧を作る(
        CheckerConfiguration? configuration = null
    )
    {
        configuration ??= new CheckerConfiguration();

        return
        [
            new 共通抽象不足規則(),
            new 型分岐ポリモーフィズム規則(),
            new 不要な抽象規則(),
            new 過剰可視性規則(),
            new 封止候補規則(),
            new 静的メンバー候補規則(),
            new 静的クラス候補規則(),
            new 状態保持静的設計規則(),
            new カプセル化漏出規則(),
            new 不変条件迂回規則(),
            new 外部状態操作過剰規則(),
            new 過大主処理規則(),
            new 疑わしい継承関係規則(),
            new 親契約未使用規則(),
            new 子型による親動作無効化規則(),
            new 継承深度過剰規則(configuration.RuleSettings.Oop304.WarningDepth),
            new 具象型依存規則(),
            new 回避可能な具象生成規則(),
            new コンポジション候補規則(),
            new 複数オブジェクト混在規則(),
            new 貧血オブジェクト規則(),
            new 過剰無関係依存規則(),
            new 過剰オブジェクト辿り規則(),
            new 取得設定のみオブジェクト規則(),
        ];
    }
}
