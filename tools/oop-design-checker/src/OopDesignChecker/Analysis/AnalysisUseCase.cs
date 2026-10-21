using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": "注入された依存を順に呼び、1回の解析結果を組み立てます。",
  "フィールド": {"_入力を読み込む": "対象と設定を取得する外部入力境界です。", "_規則群を取得する": "設定に応じた規則を供給します。", "_読み込み器を生成する": "無視パスに応じたローダーを供給します。", "_診断群を生成する": "選択された規則でプロジェクト群を解析します。"}
}
*/
internal sealed class 解析ユースケース
{
    private readonly Func<string, string?, 解析入力> _入力を読み込む;
    private readonly Func<CheckerConfiguration, IReadOnlyList<I解析規則>> _規則群を取得する;
    private readonly Func<IReadOnlyList<string>, Iプロジェクト読み込み器> _読み込み器を生成する;
    private readonly Func<
        IReadOnlyList<I解析規則>,
        IReadOnlyList<解析対象プロジェクト>,
        CancellationToken,
        IReadOnlyList<DesignDiagnostic>
    > _診断群を生成する;

    /*
    {
      "責務": "生成方法を決めずに、解析が必要とする4つの依存を受け取ります。",
      "処理": ["nullの依存を拒否します。", "1回の解析で使用する関数を保持します。"],
      "引数": {"入力を読み込む": "対象と設定を取得します。", "規則群を取得する": "設定から規則を供給します。", "読み込み器を生成する": "無視パスからローダーを供給します。", "診断群を生成する": "規則、プロジェクト、キャンセル通知から診断を生成します。"},
      "戻り値": []
    }
    */
    public 解析ユースケース(
        Func<string, string?, 解析入力> 入力を読み込む,
        Func<CheckerConfiguration, IReadOnlyList<I解析規則>> 規則群を取得する,
        Func<IReadOnlyList<string>, Iプロジェクト読み込み器> 読み込み器を生成する,
        Func<
            IReadOnlyList<I解析規則>,
            IReadOnlyList<解析対象プロジェクト>,
            CancellationToken,
            IReadOnlyList<DesignDiagnostic>
        > 診断群を生成する
    )
    {
        ArgumentNullException.ThrowIfNull(入力を読み込む);
        ArgumentNullException.ThrowIfNull(規則群を取得する);
        ArgumentNullException.ThrowIfNull(読み込み器を生成する);
        ArgumentNullException.ThrowIfNull(診断群を生成する);
        _入力を読み込む = 入力を読み込む;
        _規則群を取得する = 規則群を取得する;
        _読み込み器を生成する = 読み込み器を生成する;
        _診断群を生成する = 診断群を生成する;
    }

    /*
    {
      "責務": "依存の生成方法に関与せず、解析の順序と結果の整合性を保証します。",
      "処理": ["キャンセルを確認し、対象と設定を取得します。", "無効な規則を除き、対象プロジェクトを読み込みます。", "診断を生成し、設定値と実効失敗基準を結果へ保存します。"],
      "引数": {"対象パス": "外部入力境界へそのまま渡す対象です。", "設定パス": "明示指定する設定パスです。", "失敗基準の上書き": "実効失敗基準だけを変更する指定です。", "キャンセル通知": "各段階へ渡すキャンセル通知です。"},
      "戻り値": "解析完了時点の診断と設定を保持する結果です。"
    }
    */
    public CheckerRunResult 実行する(
        string 対象パス,
        string? 設定パス = null,
        DesignDiagnosticSeverity? 失敗基準の上書き = null,
        CancellationToken キャンセル通知 = default
    )
    {
        // ファイルシステム等への接触より前に、呼び出し済みのキャンセルを通知します。
        キャンセル通知.ThrowIfCancellationRequested();
        var 入力 = _入力を読み込む(対象パス, 設定パス);
        var 設定 = 入力.読込済み設定.Configuration;
        キャンセル通知.ThrowIfCancellationRequested();

        // 設定の解釈はユースケースが担い、規則やローダーの具体型は依存側が決めます。
        var 無効な規則群 = 設定.DisabledRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var 規則群 = _規則群を取得する(設定)
            .Where(規則 => !無効な規則群.Contains(規則.規則情報.Id))
            .ToArray();
        var 読み込み器 = _読み込み器を生成する(設定.IgnoredPaths);
        var プロジェクト群 = 読み込み器.プロジェクト群を読み込む(
            入力.対象の絶対パス,
            キャンセル通知
        );
        キャンセル通知.ThrowIfCancellationRequested();

        // 設定ファイルの基準値と、上書き後の実効基準をそれぞれ保持します。
        var 診断群 = _診断群を生成する(規則群, プロジェクト群, キャンセル通知);
        var 失敗基準 = 失敗基準の上書き ?? 設定.FailureThreshold;
        return new CheckerRunResult(診断群, 失敗基準, 入力.読込済み設定.Path)
        {
            Configuration = 設定,
        };
    }
}
