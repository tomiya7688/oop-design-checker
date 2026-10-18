using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": "集計対象として選ばれた構文から、循環的複雑度だけを計算します。",
  "フィールド": []
}
*/
internal static class 循環的複雑度計算器
{
    /*
    {
      "責務": "OOP201の既存の分岐加点を合計します。",
      "処理": ["直線処理の基準値1から始めます。", "対象構文の分岐を加点します。"],
      "引数": {"対象ノード群": "ローカル関数と匿名関数の内部を除外済みの構文一覧です。"},
      "戻り値": "循環的複雑度です。"
    }
    */
    public static int 計算する(IReadOnlyList<SyntaxNode> 対象ノード群)
    {
        var 複雑度 = 1;

        foreach (var 構文 in 対象ノード群)
        {
            複雑度 += 構文 switch
            {
                IfStatementSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                WhileStatementSyntax => 1,
                DoStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                CasePatternSwitchLabelSyntax => 1,
                SwitchExpressionArmSyntax => 1,
                CatchClauseSyntax => 1,
                ConditionalExpressionSyntax => 1,
                BinaryExpressionSyntax 二項式 when 二項式.IsKind(SyntaxKind.LogicalAndExpression) =>
                    1,
                BinaryExpressionSyntax 二項式 when 二項式.IsKind(SyntaxKind.LogicalOrExpression) =>
                    1,
                _ => 0,
            };
        }

        return 複雑度;
    }
}
