using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 不要な抽象規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP003", "Unnecessary abstraction candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        var インターフェース群 = ソースインターフェース群を読む(解析文脈).ToArray();
        var クラス群 = ソースクラス群を読む(解析文脈).ToArray();

        foreach (var インターフェース情報 in インターフェース群)
        {
            if (
                外部拡張点か(インターフェース情報.Symbol)
                || !プロジェクト抽象判定器.意味のある抽象である(インターフェース情報.Symbol)
            )
            {
                continue;
            }

            var 実装クラス群 = クラス群.Where(クラス情報 =>
                    クラス情報.Symbol.AllInterfaces.Any(実装先 =>
                        SymbolEqualityComparer.Default.Equals(実装先, インターフェース情報.Symbol)
                    )
                )
                .ToArray();
            if (
                実装クラス群.Length != 1
                || 実装外に型付き利用があるか(
                    解析文脈,
                    インターフェース情報.Symbol,
                    実装クラス群[0].Symbol
                )
            )
            {
                continue;
            }

            yield return 診断生成器.生成する(
                規則情報,
                インターフェース情報.Declaration.Identifier.GetLocation(),
                $"Internal abstraction {インターフェース情報.Symbol.Name} has one 実装クラス and is not used as a declared type outside that 実装クラス. The abstraction currently adds ceremony without enabling polymorphic use.",
                インターフェース情報.Symbol.ToDisplayString()
            );
        }
    }

    private static bool 外部拡張点か(INamedTypeSymbol 対象インターフェース) =>
        対象インターフェース.DeclaredAccessibility
            is Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal
                or Accessibility.ProtectedAndInternal;

    private static IEnumerable<インターフェース情報> ソースインターフェース群を読む(
        解析文脈 解析文脈
    )
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var 宣言 in 構文木.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            )
            {
                if (意味モデル.GetDeclaredSymbol(宣言) is INamedTypeSymbol 型記号)
                {
                    yield return new インターフェース情報(宣言, 型記号);
                }
            }
        }
    }

    private static IEnumerable<クラス情報> ソースクラス群を読む(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var 宣言 in 構文木.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            )
            {
                if (意味モデル.GetDeclaredSymbol(宣言) is INamedTypeSymbol 型記号)
                {
                    yield return new クラス情報(型記号);
                }
            }
        }
    }

    private static bool 実装外に型付き利用があるか(
        解析文脈 解析文脈,
        INamedTypeSymbol 対象インターフェース,
        INamedTypeSymbol 実装クラス
    )
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (var 名前構文 in 構文木.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (
                    !SymbolEqualityComparer.Default.Equals(
                        意味モデル.GetSymbolInfo(名前構文).Symbol,
                        対象インターフェース
                    )
                )
                {
                    continue;
                }

                if (名前構文.Ancestors().OfType<BaseListSyntax>().Any())
                {
                    continue;
                }

                var 包含型 = 意味モデル.GetEnclosingSymbol(名前構文.SpanStart)?.ContainingType;
                if (!SymbolEqualityComparer.Default.Equals(包含型, 実装クラス))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record インターフェース情報(
        InterfaceDeclarationSyntax Declaration,
        INamedTypeSymbol Symbol
    );

    private sealed record クラス情報(INamedTypeSymbol Symbol);
}
