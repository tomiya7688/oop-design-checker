using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "ファイルとMSBuildを使わず、解析ユースケースの依存呼び出しと引数を記録します。",
  "フィールド": {"呼出順序": "実行された段階です。", "段階の処理": "例外やキャンセルを発生させるテスト用処理です。", "設定": "入力境界が返す設定です。", "規則群": "大文字小文字を区別しない無効化を検証する規則です。", "プロジェクト群": "メモリ上で作成したプロジェクトです。", "診断群": "解析依存が返す診断です。", "指定対象": "入力境界へ渡された対象です。", "指定設定パス": "入力境界へ渡された設定パスです。", "規則供給時の設定": "規則依存へ渡された設定です。", "ローダー用の無視パス群": "ローダー生成へ渡された除外設定です。", "読込対象": "ローダーへ渡された解決済み対象です。", "読込時の通知": "ローダーへ渡されたキャンセル通知です。", "実行規則群": "診断依存へ渡された規則です。", "実行プロジェクト群": "診断依存へ渡されたプロジェクトです。", "解析時の通知": "診断依存へ渡されたキャンセル通知です。"}
}
*/
internal sealed class 解析ユースケース検証環境 : Iプロジェクト読み込み器
{
    public List<string> 呼出順序 { get; } = [];
    public Action<string>? 段階の処理 { get; set; }
    public CheckerConfiguration 設定 { get; } =
        new()
        {
            IgnoredPaths = ["**/Generated/**"],
            DisabledRules = ["skip"],
            FailureThreshold = DesignDiagnosticSeverity.Warning,
        };
    public IReadOnlyList<I解析規則> 規則群 { get; } =
    [new テスト解析規則("KEEP"), new テスト解析規則("SKIP")];
    public IReadOnlyList<解析対象プロジェクト> プロジェクト群 { get; } =
    [
        new(
            "virtual-root",
            false,
            CSharpCompilation.Create("Memory"),
            new Dictionary<SyntaxTree, SemanticModel>()
        ),
    ];
    public IReadOnlyList<DesignDiagnostic> 診断群 { get; } =
    [
        new(
            new("KEEP", "test", DesignDiagnosticSeverity.Attention),
            DesignDiagnosticSeverity.Attention,
            "test",
            null,
            new("virtual.cs", 1, 1)
        ),
    ];
    public string? 指定対象 { get; private set; }
    public string? 指定設定パス { get; private set; }
    public CheckerConfiguration? 規則供給時の設定 { get; private set; }
    public IReadOnlyList<string>? ローダー用の無視パス群 { get; private set; }
    public string? 読込対象 { get; private set; }
    public CancellationToken 読込時の通知 { get; private set; }
    public IReadOnlyList<I解析規則>? 実行規則群 { get; private set; }
    public IReadOnlyList<解析対象プロジェクト>? 実行プロジェクト群 { get; private set; }
    public CancellationToken 解析時の通知 { get; private set; }

    /*
    {
      "責務": "記録用の依存をまとめて注入します。",
      "処理": ["4つの記録用関数で解析ユースケースを構築します。"],
      "引数": [],
      "戻り値": "ファイルやMSBuildに触れないユースケースです。"
    }
    */
    public 解析ユースケース 作る() =>
        new(入力を読み込む, 規則群を取得する, 読み込み器を生成する, 診断群を生成する);

    /*
    {
      "責務": "入力境界の呼び出しを記録します。",
      "処理": ["元の引数を記録します。", "用意した設定と仮想パスを返します。"],
      "引数": {"対象パス": "元の対象です。", "設定パス": "元の設定パスです。"},
      "戻り値": "テスト用の解析入力です。"
    }
    */
    public 解析入力 入力を読み込む(string 対象パス, string? 設定パス)
    {
        指定対象 = 対象パス;
        指定設定パス = 設定パス;
        段階を記録する("入力");
        return new("virtual-resolved-target", new(設定, "virtual-resolved-config"));
    }

    /*
    {
      "責務": "規則の供給を記録します。",
      "処理": ["渡された設定を記録し、固定の規則を返します。"],
      "引数": {"使用設定": "入力境界から取得した設定です。"},
      "戻り値": "無効化前の規則です。"
    }
    */
    public IReadOnlyList<I解析規則> 規則群を取得する(CheckerConfiguration 使用設定)
    {
        規則供給時の設定 = 使用設定;
        段階を記録する("規則");
        return 規則群;
    }

    /*
    {
      "責務": "ローダー生成の引数を記録します。",
      "処理": ["除外設定を記録し、この記録用ローダーを返します。"],
      "引数": {"無視パス群": "設定モデルの除外一覧です。"},
      "戻り値": "プロジェクトをメモリから返すローダーです。"
    }
    */
    public Iプロジェクト読み込み器 読み込み器を生成する(IReadOnlyList<string> 無視パス群)
    {
        ローダー用の無視パス群 = 無視パス群;
        段階を記録する("ローダー生成");
        return this;
    }

    /*
    {
      "責務": "解決済み対象とキャンセル通知の受け渡しを記録します。",
      "処理": ["引数を記録し、メモリ上のプロジェクト一覧を返します。"],
      "引数": {"対象パス": "解決済み対象です。", "キャンセル通知": "呼び出し元の通知です。"},
      "戻り値": "固定のプロジェクト一覧です。"
    }
    */
    public IReadOnlyList<解析対象プロジェクト> プロジェクト群を読み込む(
        string 対象パス,
        CancellationToken キャンセル通知 = default
    )
    {
        読込対象 = 対象パス;
        読込時の通知 = キャンセル通知;
        段階を記録する("プロジェクト");
        return プロジェクト群;
    }

    /*
    {
      "責務": "規則選択後の解析引数とキャンセル通知を記録します。",
      "処理": ["解析引数を記録します。", "キャンセルを確認して診断を返します。"],
      "引数": {"使用規則群": "無効規則を除いた一覧です。", "使用プロジェクト群": "ローダーの応答です。", "キャンセル通知": "呼び出し元の通知です。"},
      "戻り値": "固定の診断一覧です。"
    }
    */
    public IReadOnlyList<DesignDiagnostic> 診断群を生成する(
        IReadOnlyList<I解析規則> 使用規則群,
        IReadOnlyList<解析対象プロジェクト> 使用プロジェクト群,
        CancellationToken キャンセル通知
    )
    {
        実行規則群 = 使用規則群;
        実行プロジェクト群 = 使用プロジェクト群;
        解析時の通知 = キャンセル通知;
        段階を記録する("診断");
        キャンセル通知.ThrowIfCancellationRequested();
        return 診断群;
    }

    /*
    {
      "責務": "依存の実行順と、そこで発生した中断を観測します。",
      "処理": ["段階名を記録します。", "指定された中断処理を実行します。"],
      "引数": {"段階": "現在の依存の名前です。"},
      "戻り値": []
    }
    */
    private void 段階を記録する(string 段階)
    {
        呼出順序.Add(段階);
        段階の処理?.Invoke(段階);
    }

    /*
    {
      "責務": "規則選択だけを検証するためのメタデータを保持します。",
      "フィールド": {"規則情報": "選択を確認する識別子を持つ規則情報です。"}
    }
    */
    private sealed class テスト解析規則(string 識別子) : I解析規則
    {
        public RuleDescriptor 規則情報 { get; } =
            new(識別子, "test", DesignDiagnosticSeverity.Attention);

        /*
        {
          "責務": "規則選択テストでは実解析を行いません。",
          "処理": ["空の診断を返します。"],
          "引数": {"context": "未使用の解析文脈です。"},
          "戻り値": "空の診断一覧です。"
        }
        */
        public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context) => [];
    }
}
