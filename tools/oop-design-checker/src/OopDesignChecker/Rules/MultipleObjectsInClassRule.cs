using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

/*
{
  "責務": {
    "複数オブジェクト混在規則": "状態メンバーと操作の独立したまとまりを判定します。状態の選別と閾値を所有し、helper到達可能性は共有グラフに委譲します。"
  }
}
*/
internal sealed class 複数オブジェクト混在規則 : I解析規則
{
    private const int まとまりあたり最小状態メンバー数 = 3;
    private const int まとまりあたり最小メソッド数 = 3;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP401", "Possible multiple objects in one class", DesignDiagnosticSeverity.Warning);

    /*
    {
      "責務": {
        "解析する": "状態メンバーと操作の独立したまとまりを判定します。状態の選別と閾値を所有し、helper到達可能性は共有グラフに委譲します。"
      },
      "処理": [
        "対象クラスのメンバー利用を共有グラフで集計します。",
        "規則固有の接続と閾値を判定して警告を生成します。"
      ],
      "引数": {
        "文脈": "解析するプロジェクトと周辺情報です。"
      },
      "戻り値": "条件を満たしたクラスの診断です。"
    }
    */
    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (var 宣言 in 構文ルート.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol 記号
                    || !シンボル操作器.主宣言である(記号, 宣言)
                )
                {
                    continue;
                }

                // 状態の選別は規則が所有し、helper呼出の追跡は共有グラフに任せます。
                var 利用グラフ = new 同一クラスメソッド利用グラフ(記号, 宣言, 意味モデル);
                var 状態使用状況 = 利用グラフ.有効利用を集計する<ISymbol>(メンバー =>
                    インスタンス状態メンバーである(メンバー, 記号)
                );
                var まとまり群 = 独立まとまりを探す(状態使用状況);
                if (まとまり群.Length < 2)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"This class contains {まとまり群.Length} independent state/behavior まとまり群. The まとまり群 do not share instance state and each has multiple state members and operations, which suggests more than one object may be represented here.",
                    記号.ToDisplayString()
                );
            }
        }
    }

    private static bool インスタンス状態メンバーである(ISymbol? 記号, INamedTypeSymbol 包含型) =>
        記号 switch
        {
            IFieldSymbol フィールド => !フィールド.IsStatic
                && SymbolEqualityComparer.Default.Equals(フィールド.ContainingType, 包含型),
            IPropertySymbol プロパティ => !プロパティ.IsStatic
                && SymbolEqualityComparer.Default.Equals(プロパティ.ContainingType, 包含型),
            _ => false,
        };

    private static 状態まとまり[] 独立まとまりを探す(
        IReadOnlyDictionary<IMethodSymbol, HashSet<ISymbol>> 状態使用状況
    )
    {
        var 全状態メンバー群 = new HashSet<ISymbol>(
            状態使用状況.Values.SelectMany(状態メンバー群 => 状態メンバー群),
            SymbolEqualityComparer.Default
        );
        var 未処理状態メンバー群 = new HashSet<ISymbol>(
            全状態メンバー群,
            SymbolEqualityComparer.Default
        );
        var まとまり群 = new List<状態まとまり>();

        while (未処理状態メンバー群.Count > 0)
        {
            var 先頭状態メンバー = 未処理状態メンバー群.First();
            var 状態メンバー群 = 連結状態を広げる(
                先頭状態メンバー,
                状態使用状況,
                未処理状態メンバー群
            );
            var メソッド群 = new HashSet<IMethodSymbol>(
                状態使用状況
                    .Where(pair => pair.Value.Any(状態メンバー群.Contains))
                    .Select(pair => pair.Key),
                SymbolEqualityComparer.Default
            );

            if (
                状態メンバー群.Count >= まとまりあたり最小状態メンバー数
                && メソッド群.Count >= まとまりあたり最小メソッド数
            )
            {
                まとまり群.Add(new 状態まとまり(状態メンバー群.Count, メソッド群.Count));
            }
        }

        return まとまり群.ToArray();
    }

    private static HashSet<ISymbol> 連結状態を広げる(
        ISymbol 先頭状態メンバー,
        IReadOnlyDictionary<IMethodSymbol, HashSet<ISymbol>> 状態使用状況,
        HashSet<ISymbol> 未処理状態メンバー群
    )
    {
        var 結果 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var 待ち行列 = new Queue<ISymbol>();
        待ち行列.Enqueue(先頭状態メンバー);
        未処理状態メンバー群.Remove(先頭状態メンバー);

        while (待ち行列.TryDequeue(out var 現在の項目))
        {
            結果.Add(現在の項目);
            foreach (
                var 状態メンバー群 in 状態使用状況.Values.Where(items => items.Contains(現在の項目))
            )
            {
                foreach (var 関連状態メンバー in 状態メンバー群)
                {
                    if (未処理状態メンバー群.Remove(関連状態メンバー))
                    {
                        待ち行列.Enqueue(関連状態メンバー);
                    }
                }
            }
        }

        return 結果;
    }

    private readonly record struct 状態まとまり(int 状態メンバー数, int メソッド数);
}
