using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

/*
{
  "責務": {
    "過剰無関係依存規則": "独立した依存群を判定します。依存候補・共通抽象の接続・群の閾値を所有し、helper到達可能性は共有グラフに委譲します。"
  }
}
*/
internal sealed class 過剰無関係依存規則 : I解析規則
{
    private const int 最低依存群数 = 3;
    private const int 依存群ごとの最低依存数 = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP403", "Excessive unrelated dependencies", DesignDiagnosticSeverity.Warning);

    /*
    {
      "責務": {
        "解析する": "独立した依存群を判定します。依存候補・共通抽象の接続・群の閾値を所有し、helper到達可能性は共有グラフに委譲します。"
      },
      "処理": [
        "対象クラスのメンバー利用を共有グラフで集計します。",
        "規則固有の接続と閾値を判定して警告を生成します。"
      ],
      "引数": {
        "解析文脈": "解析するプロジェクトと周辺情報です。"
      },
      "戻り値": "条件を満たしたクラスの診断です。"
    }
    */
    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 宣言 in 構文根.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol シンボル)
                {
                    continue;
                }

                var 依存先群 = 依存フィールドを読む(シンボル);
                if (依存先群.Length < 最低依存群数 * 依存群ごとの最低依存数)
                {
                    continue;
                }

                // 依存候補の条件とクラスタ判定は、この規則に残します。
                var 依存先集合 = new HashSet<IFieldSymbol>(
                    依存先群,
                    SymbolEqualityComparer.Default
                );
                var 利用グラフ = new 同一クラスメソッド利用グラフ(シンボル, 宣言, 意味モデル);
                var 実効使用状況 = 利用グラフ.有効利用を集計する<IFieldSymbol>(依存先集合.Contains);
                var 依存群数 = 依存群数を数える(依存先群, 実効使用状況);
                if (依存群数 < 最低依存群数)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"This object contains {依存群数} independent groups of project dependencies. Distinct dependency clusters often indicate several separate object concerns are being coordinated inside one object.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }

    private static IFieldSymbol[] 依存フィールドを読む(INamedTypeSymbol 型) =>
        型.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(フィールド =>
                !フィールド.IsStatic
                && フィールド.Type is INamedTypeSymbol namedType
                && namedType.SpecialType == SpecialType.None
                && namedType.Locations.Any(location => location.IsInSource)
            )
            .ToArray();

    private static int 依存群数を数える(
        IReadOnlyCollection<IFieldSymbol> 依存先群,
        IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> 使用状況
    )
    {
        var 残り要素 = new HashSet<IFieldSymbol>(依存先群, SymbolEqualityComparer.Default);
        var 条件を満たす依存群数 = 0;

        while (残り要素.Count > 0)
        {
            var 探索待ち行列 = new Queue<IFieldSymbol>();
            var 依存群 = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
            var 先頭要素 = 残り要素.First();
            残り要素.Remove(先頭要素);
            探索待ち行列.Enqueue(先頭要素);

            while (探索待ち行列.TryDequeue(out var 現在の要素))
            {
                依存群.Add(現在の要素);
                メソッド使用状況で連結する(現在の要素, 使用状況, 残り要素, 探索待ち行列);
                共通抽象で連結する(現在の要素, 残り要素, 探索待ち行列);
            }

            if (
                依存群.Count >= 依存群ごとの最低依存数
                && 使用状況.Values.Any(fields => fields.Any(依存群.Contains))
            )
            {
                条件を満たす依存群数++;
            }
        }

        return 条件を満たす依存群数;
    }

    private static void メソッド使用状況で連結する(
        IFieldSymbol 現在の要素,
        IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> 使用状況,
        HashSet<IFieldSymbol> 残り要素,
        Queue<IFieldSymbol> 探索待ち行列
    )
    {
        foreach (
            var メソッド使用フィールド群 in 使用状況.Values.Where(fields =>
                fields.Contains(現在の要素)
            )
        )
        {
            foreach (var 関連要素 in メソッド使用フィールド群)
            {
                if (残り要素.Remove(関連要素))
                {
                    探索待ち行列.Enqueue(関連要素);
                }
            }
        }
    }

    private static void 共通抽象で連結する(
        IFieldSymbol 現在の要素,
        HashSet<IFieldSymbol> 残り要素,
        Queue<IFieldSymbol> 探索待ち行列
    )
    {
        var 関連フィールド群 = 残り要素.Where(候補 =>
                意味のあるプロジェクト抽象を共有する(現在の要素.Type, 候補.Type)
            )
            .ToArray();

        foreach (var 関連要素 in 関連フィールド群)
        {
            if (残り要素.Remove(関連要素))
            {
                探索待ち行列.Enqueue(関連要素);
            }
        }
    }

    private static bool 意味のあるプロジェクト抽象を共有する(ITypeSymbol 左辺, ITypeSymbol 右辺)
    {
        if (左辺 is not INamedTypeSymbol 左辺型 || 右辺 is not INamedTypeSymbol 右辺型)
        {
            return false;
        }

        var 左辺型階層 = プロジェクト型階層を読む(左辺型);
        return プロジェクト型階層を読む(右辺型).Any(左辺型階層.Contains);
    }

    private static HashSet<INamedTypeSymbol> プロジェクト型階層を読む(INamedTypeSymbol 型)
    {
        var 結果 = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        for (
            INamedTypeSymbol? 現在の要素 = 型;
            現在の要素 is not null && 現在の要素.SpecialType != SpecialType.System_Object;
            現在の要素 = 現在の要素.BaseType
        )
        {
            if (現在の要素.Locations.Any(location => location.IsInSource))
            {
                結果.Add(現在の要素);
            }
        }

        foreach (
            var 抽象 in 型.AllInterfaces.Where(候補 =>
                候補.Locations.Any(location => location.IsInSource)
            )
        )
        {
            結果.Add(抽象);
        }

        return 結果;
    }
}
