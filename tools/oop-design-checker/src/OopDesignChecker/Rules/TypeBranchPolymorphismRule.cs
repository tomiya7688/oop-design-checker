using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 型分岐ポリモーフィズム規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP002", "Polymorphism bypassed by type branching", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 条件分岐 in 構文根.DescendantNodes().OfType<IfStatementSyntax>())
            {
                if (else付き条件分岐である(条件分岐))
                {
                    continue;
                }

                var 型判定群 = 型判定群を集める(条件分岐, 意味モデル).ToArray();
                var 疑わしい型判定群 = 型判定群.GroupBy(
                        型判定 => 型判定.対象式,
                        StringComparer.Ordinal
                    )
                    .FirstOrDefault(同一対象群 =>
                        共通のポリモーフィズム契約を持つ(同一対象群.ToArray())
                    );

                if (疑わしい型判定群 is null)
                {
                    continue;
                }

                var 型名群 = string.Join(
                    ", ",
                    疑わしい型判定群
                        .Select(型判定 => 型判定.型.Name)
                        .Distinct(StringComparer.Ordinal)
                );
                yield return 診断生成器.生成する(
                    規則情報,
                    条件分岐.IfKeyword.GetLocation(),
                    $"Repeated runtime type branching ({型名群}) bypasses a shared source-defined type contract and may be replaceable with polymorphic behavior."
                );
            }
        }
    }

    private static bool else付き条件分岐である(IfStatementSyntax 条件文) =>
        条件文.Parent is ElseClauseSyntax { Statement: IfStatementSyntax };

    private static IEnumerable<型判定情報> 型判定群を集める(
        IfStatementSyntax 構文根,
        SemanticModel 意味モデル
    )
    {
        IfStatementSyntax? 現在の条件分岐 = 構文根;
        while (現在の条件分岐 is not null)
        {
            if (型判定を読み取る(現在の条件分岐.Condition, 意味モデル, out var 型判定))
            {
                yield return 型判定;
            }

            現在の条件分岐 = 現在の条件分岐.Else?.Statement as IfStatementSyntax;
        }
    }

    private static bool 型判定を読み取る(
        ExpressionSyntax condition,
        SemanticModel 意味モデル,
        out 型判定情報 型判定
    )
    {
        if (
            condition is BinaryExpressionSyntax 型判定式
            && 型判定式.RawKind == (int)SyntaxKind.IsExpression
            && 意味モデル.GetTypeInfo(型判定式.Right).Type is INamedTypeSymbol 判定対象型
        )
        {
            型判定 = new 型判定情報(型判定式.Left.ToString(), 判定対象型);
            return true;
        }

        if (condition is IsPatternExpressionSyntax 型パターン式)
        {
            var 型構文 = 型パターン式.Pattern switch
            {
                TypePatternSyntax typePattern => typePattern.Type,
                DeclarationPatternSyntax declarationPattern => declarationPattern.Type,
                _ => null,
            };

            if (
                型構文 is not null
                && 意味モデル.GetTypeInfo(型構文).Type is INamedTypeSymbol パターン対象型
            )
            {
                型判定 = new 型判定情報(型パターン式.Expression.ToString(), パターン対象型);
                return true;
            }
        }

        型判定 = default;
        return false;
    }

    private static bool 共通のポリモーフィズム契約を持つ(IReadOnlyList<型判定情報> 型判定群)
    {
        var 型群 = new List<INamedTypeSymbol>();
        foreach (var 型判定 in 型判定群)
        {
            if (
                型判定.型.TypeKind != TypeKind.Class
                || !型判定.型.Locations.Any(location => location.IsInSource)
                || 型群.Any(既存型 => SymbolEqualityComparer.Default.Equals(既存型, 型判定.型))
            )
            {
                continue;
            }

            型群.Add(型判定.型);
        }

        if (型群.Count < 2)
        {
            return false;
        }

        foreach (var 候補 in ソース定義クラス契約を列挙する(型群[0]))
        {
            if (型群.Skip(1).All(型 => シンボル操作器.同じ型か基底型である(候補, 型)))
            {
                return true;
            }
        }

        foreach (
            var インターフェース型 in 型群[0]
                .AllInterfaces.Where(プロジェクト抽象判定器.意味のある抽象である)
        )
        {
            if (
                型群.Skip(1)
                    .All(型 =>
                        型.AllInterfaces.Any(実装済みインターフェース =>
                            SymbolEqualityComparer.Default.Equals(
                                実装済みインターフェース,
                                インターフェース型
                            )
                        )
                    )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> ソース定義クラス契約を列挙する(INamedTypeSymbol 型)
    {
        INamedTypeSymbol? current = 型;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Locations.Any(location => location.IsInSource))
            {
                yield return current;
            }

            current = current.BaseType;
        }
    }

    private readonly record struct 型判定情報(string 対象式, INamedTypeSymbol 型);
}
