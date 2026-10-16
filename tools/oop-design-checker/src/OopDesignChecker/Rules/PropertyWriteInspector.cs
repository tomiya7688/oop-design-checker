using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Rules;

internal static class プロパティ書き込み検査器
{
    public static プロパティ書き込み範囲 書き込み範囲を調べる(
        解析文脈 文脈,
        IPropertySymbol プロパティ
    )
    {
        var 内部書き込みがある = false;
        var 外部書き込みがある = false;

        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (
                var 代入式 in 構文ルート.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            )
            {
                if (!対象プロパティである(意味モデル, 代入式.Left, プロパティ))
                {
                    continue;
                }

                書き込みを分類する(
                    意味モデル,
                    代入式.SpanStart,
                    プロパティ,
                    ref 内部書き込みがある,
                    ref 外部書き込みがある
                );
            }

            foreach (
                var 単項式 in 構文ルート.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>()
            )
            {
                if (対象プロパティである(意味モデル, 単項式.Operand, プロパティ))
                {
                    書き込みを分類する(
                        意味モデル,
                        単項式.SpanStart,
                        プロパティ,
                        ref 内部書き込みがある,
                        ref 外部書き込みがある
                    );
                }
            }

            foreach (
                var 単項式 in 構文ルート.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>()
            )
            {
                if (対象プロパティである(意味モデル, 単項式.Operand, プロパティ))
                {
                    書き込みを分類する(
                        意味モデル,
                        単項式.SpanStart,
                        プロパティ,
                        ref 内部書き込みがある,
                        ref 外部書き込みがある
                    );
                }
            }
        }

        return new プロパティ書き込み範囲(内部書き込みがある, 外部書き込みがある);
    }

    private static bool 対象プロパティである(
        SemanticModel 意味モデル,
        ExpressionSyntax expression,
        IPropertySymbol プロパティ
    ) =>
        SymbolEqualityComparer.Default.Equals(
            意味モデル.GetSymbolInfo(expression).Symbol,
            プロパティ
        );

    private static void 書き込みを分類する(
        SemanticModel 意味モデル,
        int 位置,
        IPropertySymbol プロパティ,
        ref bool 内部書き込みがある,
        ref bool 外部書き込みがある
    )
    {
        var 書き込み型 = 意味モデル.GetEnclosingSymbol(位置)?.ContainingType;
        if (SymbolEqualityComparer.Default.Equals(書き込み型, プロパティ.ContainingType))
        {
            内部書き込みがある = true;
        }
        else
        {
            外部書き込みがある = true;
        }
    }
}

internal readonly record struct プロパティ書き込み範囲(
    bool 内部書き込みがある,
    bool 外部書き込みがある
);
