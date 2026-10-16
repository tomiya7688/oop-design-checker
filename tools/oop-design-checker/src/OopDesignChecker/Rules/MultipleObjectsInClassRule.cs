using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 複数オブジェクト混在規則 : I解析規則
{
    private const int まとまりあたり最小状態メンバー数 = 3;
    private const int まとまりあたり最小メソッド数 = 3;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP401", "Possible multiple objects in one class", DesignDiagnosticSeverity.Warning);

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

                var メソッド使用状況 = メソッド使用状況を作る(記号, 宣言, 意味モデル);
                var 状態使用状況 = 有効状態使用状況を作る(メソッド使用状況);
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

    private static Dictionary<IMethodSymbol, メソッド使用状況> メソッド使用状況を作る(
        INamedTypeSymbol 包含型,
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    )
    {
        var 結果 = new Dictionary<IMethodSymbol, メソッド使用状況>(SymbolEqualityComparer.Default);

        foreach (var メソッド宣言 in 宣言.Members.OfType<MethodDeclarationSyntax>())
        {
            if (
                意味モデル.GetDeclaredSymbol(メソッド宣言) is not IMethodSymbol メソッド
                || メソッド.IsStatic
                || メソッド.MethodKind != MethodKind.Ordinary
            )
            {
                continue;
            }

            var 状態メンバー群 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var 識別子 in メソッド宣言.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var 参照先記号 = 意味モデル.GetSymbolInfo(識別子).Symbol;
                if (インスタンス状態メンバーである(参照先記号, 包含型))
                {
                    状態メンバー群.Add(参照先記号!);
                }
            }

            var 呼び出しメソッド群 = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (
                var 呼び出し式 in メソッド宣言
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
            )
            {
                if (
                    意味モデル.GetSymbolInfo(呼び出し式).Symbol
                    is not IMethodSymbol 呼び出しメソッド
                )
                {
                    continue;
                }

                if (
                    !呼び出しメソッド.IsStatic
                    && 呼び出しメソッド.MethodKind == MethodKind.Ordinary
                    && SymbolEqualityComparer.Default.Equals(
                        呼び出しメソッド.ContainingType,
                        包含型
                    )
                )
                {
                    呼び出しメソッド群.Add(呼び出しメソッド);
                }
            }

            結果[メソッド] = new メソッド使用状況(状態メンバー群, 呼び出しメソッド群);
        }

        return 結果;
    }

    private static Dictionary<IMethodSymbol, HashSet<ISymbol>> 有効状態使用状況を作る(
        IReadOnlyDictionary<IMethodSymbol, メソッド使用状況> メソッド使用状況
    )
    {
        var 結果 = new Dictionary<IMethodSymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);

        foreach (var メソッド in メソッド使用状況.Keys)
        {
            var 有効状態群 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var 訪問済みメソッド群 = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var 待ち行列 = new Queue<IMethodSymbol>();
            待ち行列.Enqueue(メソッド);

            while (待ち行列.TryDequeue(out var 現在の項目))
            {
                if (
                    !訪問済みメソッド群.Add(現在の項目)
                    || !メソッド使用状況.TryGetValue(現在の項目, out var 使用状況)
                )
                {
                    continue;
                }

                有効状態群.UnionWith(使用状況.状態メンバー群);
                foreach (var 呼び出しメソッド in 使用状況.呼び出しメソッド群)
                {
                    待ち行列.Enqueue(呼び出しメソッド);
                }
            }

            if (有効状態群.Count > 0)
            {
                結果[メソッド] = 有効状態群;
            }
        }

        return 結果;
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

    private sealed record メソッド使用状況(
        HashSet<ISymbol> 状態メンバー群,
        HashSet<IMethodSymbol> 呼び出しメソッド群
    );

    private readonly record struct 状態まとまり(int 状態メンバー数, int メソッド数);
}
