using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 共通抽象不足規則 : I解析規則
{
    private const int 最小関連型数 = 3;
    private const int 最小共通操作数 = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP001", "Missing common abstraction", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        var 候補群 = 解析文脈
            .対象プロジェクト.構文木群.SelectMany(tree => 候補群を探す(解析文脈, tree))
            .ToArray();

        foreach (var 同一対象群 in 候補群.GroupBy(候補 => 候補.署名キー, StringComparer.Ordinal))
        {
            var 関連型群 = 同一対象群.ToArray();
            if (関連型群.Length < 最小関連型数 || !共通利用文脈を持つ(解析文脈, 関連型群))
            {
                continue;
            }

            var 型名群 = string.Join(", ", 関連型群.Select(候補 => 候補.シンボル.Name));
            yield return 診断生成器.生成する(
                規則情報,
                関連型群[0].宣言.Identifier.GetLocation(),
                $"Types {型名群} expose the same {関連型群[0].操作数} public instance operations and are used in the same call-site role, but share no project abstraction. Consider an interface or meaningful base abstraction."
            );
        }
    }

    private static IEnumerable<抽象候補情報> 候補群を探す(解析文脈 解析文脈, SyntaxTree 構文木)
    {
        var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
        var 構文根 = 構文木.GetRoot();

        foreach (var 宣言 in 構文根.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (
                意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol シンボル
                || !シンボル操作器.主宣言である(シンボル, 宣言)
                || プロジェクト内の抽象を持つ(シンボル)
            )
            {
                continue;
            }

            var signatures = シンボル.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(メソッド =>
                    メソッド.MethodKind == MethodKind.Ordinary
                    && !メソッド.IsStatic
                    && メソッド.DeclaredAccessibility == Accessibility.Public
                )
                .Select(シグネチャを作る)
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray();

            if (signatures.Length < 最小共通操作数)
            {
                continue;
            }

            yield return new 抽象候補情報(
                宣言,
                シンボル,
                string.Join("|", signatures),
                signatures.Length
            );
        }
    }

    private static bool 共通利用文脈を持つ(解析文脈 解析文脈, IReadOnlyList<抽象候補情報> 関連型群)
    {
        var methods = new List<IMethodSymbol>();

        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var 宣言 in 構文木.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            )
            {
                if (意味モデル.GetDeclaredSymbol(宣言) is IMethodSymbol メソッド)
                {
                    methods.Add(メソッド);
                }
            }
        }

        foreach (
            var 同一シグネチャ群 in methods.GroupBy(メソッド =>
                $"{メソッド.ContainingType.ToDisplayString()}|{メソッド.Name}|{メソッド.Parameters.Length}"
            )
        )
        {
            var オーバーロード群 = 同一シグネチャ群.ToArray();
            if (
                オーバーロード群.Length < 最小関連型数
                || オーバーロード群[0].Parameters.Length == 0
            )
            {
                continue;
            }

            for (var 引数位置 = 0; 引数位置 < オーバーロード群[0].Parameters.Length; 引数位置++)
            {
                var 一致型群 = new List<INamedTypeSymbol>();
                foreach (var オーバーロード in オーバーロード群)
                {
                    if (
                        オーバーロード.Parameters[引数位置].Type is not INamedTypeSymbol 引数型
                        || !関連型に一致する(引数型, 関連型群, out var 一致型)
                        || 一致型群.Any(既存型 =>
                            SymbolEqualityComparer.Default.Equals(既存型, 一致型)
                        )
                    )
                    {
                        continue;
                    }

                    一致型群.Add(一致型);
                }

                if (一致型群.Count >= 最小関連型数)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool 関連型に一致する(
        INamedTypeSymbol 型,
        IReadOnlyList<抽象候補情報> 関連型群,
        out INamedTypeSymbol 一致型
    )
    {
        foreach (var 候補 in 関連型群)
        {
            if (SymbolEqualityComparer.Default.Equals(型, 候補.シンボル))
            {
                一致型 = 候補.シンボル;
                return true;
            }
        }

        一致型 = null!;
        return false;
    }

    private static bool プロジェクト内の抽象を持つ(INamedTypeSymbol シンボル) =>
        シンボル.BaseType is { SpecialType: not SpecialType.System_Object }
        || シンボル.AllInterfaces.Any(インターフェース型 =>
            インターフェース型.Locations.Any(location => location.IsInSource)
        );

    private static string シグネチャを作る(IMethodSymbol メソッド)
    {
        var parameters = string.Join(
            ",",
            メソッド.Parameters.Select(parameter => parameter.Type.ToDisplayString())
        );
        return $"{メソッド.Name}({parameters}):{メソッド.ReturnType.ToDisplayString()}";
    }

    private sealed record 抽象候補情報(
        ClassDeclarationSyntax 宣言,
        INamedTypeSymbol シンボル,
        string 署名キー,
        int 操作数
    );
}
