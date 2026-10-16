using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 過剰可視性規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP101", "Excessive visibility", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        if (!解析文脈.対象プロジェクト.アプリケーションである)
        {
            yield break;
        }

        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 宣言 in 構文根.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol シンボル
                    || シンボル.DeclaredAccessibility != Accessibility.Public
                    || !シンボル操作器.主宣言である(シンボル, 宣言)
                    || フレームワーク契約判定器.外部型契約を持つ(シンボル)
                )
                {
                    continue;
                }

                var 参照元型群 = 参照元の型群を探す(解析文脈, シンボル)
                    .Where(型 => !SymbolEqualityComparer.Default.Equals(型, シンボル))
                    .ToArray();

                if (参照元型群.Length == 0 || 参照元型群.Any(型 => !継承関係にある(シンボル, 型)))
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    "This public class is referenced only inside its inheritance hierarchy. A narrower visibility is likely sufficient.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }

    private static List<INamedTypeSymbol> 参照元の型群を探す(
        解析文脈 解析文脈,
        INamedTypeSymbol 対象型
    )
    {
        var 参照型群 = new List<INamedTypeSymbol>();

        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 単純名 in 構文根.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var 参照先シンボル = 意味モデル.GetSymbolInfo(単純名).Symbol;
                if (!SymbolEqualityComparer.Default.Equals(参照先シンボル, 対象型))
                {
                    continue;
                }

                var 所属宣言 = 単純名.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                if (
                    所属宣言 is null
                    || 意味モデル.GetDeclaredSymbol(所属宣言) is not INamedTypeSymbol 参照元型
                    || 参照型群.Any(既存型 =>
                        SymbolEqualityComparer.Default.Equals(既存型, 参照元型)
                    )
                )
                {
                    continue;
                }

                参照型群.Add(参照元型);
            }
        }

        return 参照型群;
    }

    private static bool 継承関係にある(INamedTypeSymbol 対象型, INamedTypeSymbol 比較対象型) =>
        シンボル操作器.同じ型か基底型である(対象型, 比較対象型)
        || シンボル操作器.同じ型か基底型である(比較対象型, 対象型);
}
