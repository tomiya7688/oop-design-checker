using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 過剰無関係依存規則 : I解析規則
{
    private const int 最低依存群数 = 3;
    private const int 依存群ごとの最低依存数 = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP403", "Excessive unrelated dependencies", DesignDiagnosticSeverity.Warning);

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

                var メソッド使用状況 = メソッド使用状況を読む(シンボル, 宣言, 依存先群, 意味モデル);
                var 実効使用状況 = 実効依存使用状況を作る(メソッド使用状況);
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

    private static Dictionary<IMethodSymbol, メソッド使用状況> メソッド使用状況を読む(
        INamedTypeSymbol 所属型,
        ClassDeclarationSyntax 宣言,
        IReadOnlyCollection<IFieldSymbol> 依存先群,
        SemanticModel 意味モデル
    )
    {
        var 依存先集合 = new HashSet<IFieldSymbol>(依存先群, SymbolEqualityComparer.Default);
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

            var 使用依存先群 = new HashSet<IFieldSymbol>(
                メソッド宣言
                    .DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(identifier => 意味モデル.GetSymbolInfo(identifier).Symbol)
                    .OfType<IFieldSymbol>()
                    .Where(依存先集合.Contains),
                SymbolEqualityComparer.Default
            );

            var 呼び出し先メソッド群 = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (
                var 呼び出し in メソッド宣言.DescendantNodes().OfType<InvocationExpressionSyntax>()
            )
            {
                if (
                    意味モデル.GetSymbolInfo(呼び出し).Symbol is IMethodSymbol 呼び出し先メソッド
                    && !呼び出し先メソッド.IsStatic
                    && 呼び出し先メソッド.MethodKind == MethodKind.Ordinary
                    && SymbolEqualityComparer.Default.Equals(
                        呼び出し先メソッド.ContainingType,
                        所属型
                    )
                )
                {
                    呼び出し先メソッド群.Add(呼び出し先メソッド);
                }
            }

            結果[メソッド] = new メソッド使用状況(使用依存先群, 呼び出し先メソッド群);
        }

        return 結果;
    }

    private static Dictionary<IMethodSymbol, HashSet<IFieldSymbol>> 実効依存使用状況を作る(
        IReadOnlyDictionary<IMethodSymbol, メソッド使用状況> メソッド使用状況
    )
    {
        var 結果 = new Dictionary<IMethodSymbol, HashSet<IFieldSymbol>>(
            SymbolEqualityComparer.Default
        );

        foreach (var メソッド in メソッド使用状況.Keys)
        {
            var 依存先群 = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
            var 訪問済みメソッド群 = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var 探索待ち行列 = new Queue<IMethodSymbol>();
            探索待ち行列.Enqueue(メソッド);

            while (探索待ち行列.TryDequeue(out var 現在の要素))
            {
                if (
                    !訪問済みメソッド群.Add(現在の要素)
                    || !メソッド使用状況.TryGetValue(現在の要素, out var 使用状況)
                )
                {
                    continue;
                }

                依存先群.UnionWith(使用状況.依存先群);
                foreach (var 呼び出し先メソッド in 使用状況.呼び出し先メソッド群)
                {
                    探索待ち行列.Enqueue(呼び出し先メソッド);
                }
            }

            if (依存先群.Count > 0)
            {
                結果[メソッド] = 依存先群;
            }
        }

        return 結果;
    }

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

    private sealed record メソッド使用状況(
        HashSet<IFieldSymbol> 依存先群,
        HashSet<IMethodSymbol> 呼び出し先メソッド群
    );
}
