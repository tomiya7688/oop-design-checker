using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 過大主処理規則 : I解析規則
{
    private const int 最低文数 = 20;
    private const int 警告スコア = 4;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP201", "Oversized main operation", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var メソッド in 構文根.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(メソッド) is not IMethodSymbol メソッド記号
                    || フレームワーク管理下である(メソッド記号)
                )
                {
                    continue;
                }

                var 処理量指標 = 処理量指標計算器.計算する(メソッド, メソッド記号, 意味モデル);
                if (
                    処理量指標.文数 < 最低文数
                    || 過大スコアを計算する(処理量指標) < 警告スコア
                    || 非公開補助メソッドである(解析文脈, メソッド記号)
                )
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    メソッド.Identifier.GetLocation(),
                    $"This primary operation is structurally large: {処理量指標.行数} lines, {処理量指標.文数} statements, complexity {処理量指標.循環的複雑度}, nesting {処理量指標.最大ネスト深度}. Split meaningful processing phases into methods.",
                    メソッド記号.ToDisplayString()
                );
            }
        }
    }

    private static bool フレームワーク管理下である(IMethodSymbol メソッド) =>
        フレームワーク契約判定器.外部基底型契約を持つ(メソッド.ContainingType)
        || フレームワーク契約判定器.フレームワーク契約属性を持つ(メソッド.ContainingType)
        || フレームワーク契約判定器.フレームワーク契約属性を持つ(メソッド);

    private static int 過大スコアを計算する(処理量指標 処理量指標)
    {
        var 点数 = 0;

        点数 += 処理量指標.行数 switch
        {
            >= 120 => 2,
            >= 70 => 1,
            _ => 0,
        };
        点数 += 処理量指標.循環的複雑度 switch
        {
            >= 18 => 2,
            >= 10 => 1,
            _ => 0,
        };
        点数 += 処理量指標.最大ネスト深度 switch
        {
            >= 4 => 2,
            >= 3 => 1,
            _ => 0,
        };

        if (処理量指標.文数 >= 40)
        {
            点数++;
        }

        if (処理量指標.ローカル変数数 >= 10)
        {
            点数++;
        }

        if (処理量指標.状態メンバー数 >= 5)
        {
            点数++;
        }

        if (処理量指標.トップレベル文数 >= 15)
        {
            点数++;
        }

        return 点数;
    }

    private static bool 非公開補助メソッドである(解析文脈 解析文脈, IMethodSymbol 対象メソッド)
    {
        if (対象メソッド.DeclaredAccessibility != Accessibility.Private)
        {
            return false;
        }

        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var 呼び出し in 構文木.GetRoot()
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
            )
            {
                if (
                    意味モデル.GetSymbolInfo(呼び出し).Symbol
                        is not IMethodSymbol 呼び出されたメソッド
                    || !SymbolEqualityComparer.Default.Equals(
                        呼び出されたメソッド.OriginalDefinition,
                        対象メソッド.OriginalDefinition
                    )
                )
                {
                    continue;
                }

                var 呼び出し元宣言 = 呼び出し.Ancestors()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();
                if (
                    呼び出し元宣言 is not null
                    && 意味モデル.GetDeclaredSymbol(呼び出し元宣言) is IMethodSymbol 呼び出し元
                    && !SymbolEqualityComparer.Default.Equals(
                        呼び出し元.OriginalDefinition,
                        対象メソッド.OriginalDefinition
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }
}
