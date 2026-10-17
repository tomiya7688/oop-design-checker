using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Rules;

/*
{
  "責務": "構築後のオブジェクトを更新する利用を、型とメソッド引数に対応付けます。",
  "フィールド": {
    "_状態更新型群": "通常メソッドからインスタンス状態が更新される型です。",
    "_更新引数群": "各メソッドが直接状態を書き換える引数です。"
  }
}
*/
internal sealed class 状態更新利用索引
{
    private readonly HashSet<ISymbol> _状態更新型群 = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, HashSet<ISymbol>> _更新引数群 = new(
        SymbolEqualityComparer.Default
    );

    /*
    {
      "責務": "解析対象を一度走査し、状態更新の索引を作ります。",
      "処理": ["代入・増減・参照渡しの対象を列挙します。", "通常メソッド内の状態更新を記録します。"],
      "引数": {"文脈": "解析対象とキャンセル通知です。"},
      "戻り値": []
    }
    */
    public 状態更新利用索引(解析文脈 文脈)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            文脈.キャンセル通知.ThrowIfCancellationRequested();
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (var 構文 in 構文木.GetRoot(文脈.キャンセル通知).DescendantNodes())
            {
                var 更新対象 = 更新対象を取得する(構文);
                if (更新対象 is null)
                {
                    continue;
                }

                // 分解代入でも、左辺の各状態をシンボルとして記録します。
                foreach (var 書き込み先 in 書き込み先を列挙する(更新対象))
                {
                    文脈.キャンセル通知.ThrowIfCancellationRequested();
                    状態更新を記録する(書き込み先, 意味モデル);
                }
            }
        }
    }

    /*
    {
      "責務": "型に状態更新の利用があるかを返します。",
      "処理": ["構築型を定義へそろえて索引を参照します。"],
      "引数": {"型": "確認する型です。"},
      "戻り値": "構築後の状態更新が見つかった場合にtrueです。"
    }
    */
    public bool 状態更新がある(INamedTypeSymbol 型) =>
        _状態更新型群.Contains(型.OriginalDefinition);

    /*
    {
      "責務": "同じメソッド内で対象の引数自身が更新されるかを返します。",
      "処理": ["メソッドに対応する更新引数を確認します。"],
      "引数": {"メソッド": "引数を利用するメソッドです。", "引数": "状態の所有者を受け取る引数です。"},
      "戻り値": "引数の状態が直接更新される場合にtrueです。"
    }
    */
    public bool 引数の状態を更新する(IMethodSymbol メソッド, IParameterSymbol 引数) =>
        _更新引数群.TryGetValue(メソッド, out var 引数群) && 引数群.Contains(引数);

    /*
    {
      "責務": "既存の値を変更し得る構文から書き込み対象を取り出します。",
      "処理": ["初期化を除外し、代入・増減・ref/outの対象を選びます。"],
      "引数": {"構文": "走査中の構文です。"},
      "戻り値": "書き込み対象、または対象外の場合にnullです。"
    }
    */
    private static ExpressionSyntax? 更新対象を取得する(SyntaxNode 構文) =>
        構文 switch
        {
            AssignmentExpressionSyntax 代入 when 代入.Parent is not InitializerExpressionSyntax =>
                代入.Left,
            PrefixUnaryExpressionSyntax 前置
                when 前置.IsKind(SyntaxKind.PreIncrementExpression)
                    || 前置.IsKind(SyntaxKind.PreDecrementExpression) => 前置.Operand,
            PostfixUnaryExpressionSyntax 後置
                when 後置.IsKind(SyntaxKind.PostIncrementExpression)
                    || 後置.IsKind(SyntaxKind.PostDecrementExpression) => 後置.Operand,
            ArgumentSyntax 引数
                when 引数.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    || 引数.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) => 引数.Expression,
            _ => null,
        };

    /*
    {
      "責務": "括弧とタプルをほどき、個々の書き込み先を返します。",
      "処理": ["括弧と分解代入を再帰的に展開します。"],
      "引数": {"式": "代入などの左辺です。"},
      "戻り値": "シンボルを解決する書き込み先です。"
    }
    */
    private static IEnumerable<ExpressionSyntax> 書き込み先を列挙する(ExpressionSyntax 式) =>
        式 switch
        {
            ParenthesizedExpressionSyntax 括弧 => 書き込み先を列挙する(括弧.Expression),
            TupleExpressionSyntax タプル => タプル.Arguments.SelectMany(引数 =>
                書き込み先を列挙する(引数.Expression)
            ),
            _ => [式],
        };

    /*
    {
      "責務": "通常メソッドによるインスタンス状態の更新を索引へ登録します。",
      "処理": ["対象メンバーとメソッドを確認します。", "更新される型と、直接の受け取り引数を記録します。"],
      "引数": {"書き込み先": "状態を変更する式です。", "意味モデル": "シンボルの解決に使います。"},
      "戻り値": []
    }
    */
    private void 状態更新を記録する(ExpressionSyntax 書き込み先, SemanticModel 意味モデル)
    {
        // コンストラクターや初期化子だけで設定されるデータ形状を、状態遷移と混同しません。
        var 宣言 = 書き込み先.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var メンバー = 意味モデル.GetSymbolInfo(書き込み先).Symbol;
        if (
            宣言 is null
            || 意味モデル.GetDeclaredSymbol(宣言) is not IMethodSymbol メソッド
            || メンバー
                is not (
                    IPropertySymbol { IsStatic: false, SetMethod.IsInitOnly: false }
                    or IFieldSymbol { IsStatic: false }
                )
        )
        {
            return;
        }

        _状態更新型群.Add(メンバー.ContainingType.OriginalDefinition);
        if (
            書き込み先 is not MemberAccessExpressionSyntax アクセス
            || 意味モデル.GetSymbolInfo(アクセス.Expression).Symbol is not IParameterSymbol 引数
        )
        {
            return;
        }

        if (!_更新引数群.TryGetValue(メソッド, out var 引数群))
        {
            引数群 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            _更新引数群.Add(メソッド, 引数群);
        }
        引数群.Add(引数);
    }
}
