using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": "OOP201の測定範囲を選び、7種類の処理量指標を集計します。",
  "フィールド": []
}
*/
internal static class 処理量指標計算器
{
    /*
    {
      "責務": "対象メソッドの構造と状態参照を測定します。",
      "処理": ["入れ子の関数内部を除く構文一覧を1回だけ作ります。", "同じ一覧から指標を集計し、循環的複雑度の計算にも渡します。"],
      "引数": {"メソッド": "測定するメソッド宣言です。", "メソッド記号": "状態メンバーの所属型を確認する記号です。", "意味モデル": "状態参照の解決に使います。"},
      "戻り値": "OOP201の判定に使う7指標です。"
    }
    */
    public static 処理量指標 計算する(
        MethodDeclarationSyntax メソッド,
        IMethodSymbol メソッド記号,
        SemanticModel 意味モデル
    )
    {
        // 構文木の走査と除外条件を共有し、各指標が同じ処理範囲を測るようにします。
        var 対象ノード群 = 対象処理構文を列挙する(メソッド).ToArray();
        var 行範囲 = メソッド.GetLocation().GetLineSpan();
        var 行数 = 行範囲.EndLinePosition.Line - 行範囲.StartLinePosition.Line + 1;
        var 文数 = 対象ノード群
            .OfType<StatementSyntax>()
            .Count(文 => 文 is not LocalFunctionStatementSyntax);
        var 最大ネスト深度 = 対象ノード群
            .Where(ネスト構造である)
            .Select(構文 => ネスト深度を数える(構文, メソッド))
            .DefaultIfEmpty(0)
            .Max();
        var ローカル変数数 =
            対象ノード群.OfType<VariableDeclaratorSyntax>().Count()
            + 対象ノード群.OfType<ForEachStatementSyntax>().Count();
        var 状態メンバー数 = 状態メンバー数を数える(
            対象ノード群,
            メソッド記号.ContainingType,
            意味モデル
        );
        var トップレベル文数 =
            メソッド.Body?.Statements.Count(文 => 文 is not LocalFunctionStatementSyntax)
            ?? (メソッド.ExpressionBody is null ? 0 : 1);

        // 行数は宣言全体の行範囲を使います。入れ子の関数やコメントの行も従来どおり含みます。
        return new 処理量指標(
            行数,
            文数,
            循環的複雑度計算器.計算する(対象ノード群),
            最大ネスト深度,
            ローカル変数数,
            状態メンバー数,
            トップレベル文数
        );
    }

    /*
    {
      "責務": "外側の処理として測定する構文だけを列挙します。",
      "処理": ["宣言の子孫を走査し、ローカル関数と匿名関数の内部へは進みません。"],
      "引数": {"メソッド": "走査範囲の所有者となるメソッドです。"},
      "戻り値": "入れ子の関数内部を含まない構文です。入れ子の関数の宣言ノード自体は従来どおり列挙します。"
    }
    */
    private static IEnumerable<SyntaxNode> 対象処理構文を列挙する(
        MethodDeclarationSyntax メソッド
    ) =>
        メソッド.DescendantNodes(descendIntoChildren: 構文 =>
            構文 is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax
        );

    /*
    {
      "責務": "自身と基底型のインスタンス状態を、重複を除いて数えます。",
      "処理": ["識別子の参照先を調べます。", "所属する継承階層の非staticフィールドとプロパティを集合へ加えます。"],
      "引数": {"対象ノード群": "測定範囲の構文一覧です。", "所属型": "メソッドを宣言する型です。", "意味モデル": "参照先の解決に使います。"},
      "戻り値": "異なる状態メンバーの数です。"
    }
    */
    private static int 状態メンバー数を数える(
        IReadOnlyList<SyntaxNode> 対象ノード群,
        INamedTypeSymbol 所属型,
        SemanticModel 意味モデル
    )
    {
        var 状態メンバー群 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var 識別子 in 対象ノード群.OfType<IdentifierNameSyntax>())
        {
            var 記号 = 意味モデル.GetSymbolInfo(識別子).Symbol;
            if (
                記号 is IFieldSymbol { IsStatic: false } フィールド
                && 継承階層に属する(フィールド.ContainingType, 所属型)
            )
            {
                状態メンバー群.Add(フィールド);
                continue;
            }

            if (
                記号 is IPropertySymbol { IsStatic: false } プロパティ
                && 継承階層に属する(プロパティ.ContainingType, 所属型)
            )
            {
                状態メンバー群.Add(プロパティ);
            }
        }

        return 状態メンバー群.Count;
    }

    /*
    {
      "責務": "状態の宣言型が、メソッドの型またはその基底型かを確認します。",
      "処理": ["基底型を順に調べ、宣言型と比較します。"],
      "引数": {"宣言型": "状態メンバーを宣言する型です。", "所属型": "メソッドを宣言する型です。"},
      "戻り値": "同じ継承階層に含まれる場合にtrueです。"
    }
    */
    private static bool 継承階層に属する(INamedTypeSymbol? 宣言型, INamedTypeSymbol 所属型)
    {
        for (var 現在の型 = 所属型; 現在の型 is not null; 現在の型 = 現在の型.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(現在の型, 宣言型))
            {
                return true;
            }
        }

        return false;
    }

    /*
    {
      "責務": "対象構文が処理内の何段目の制御構造にあるかを数えます。",
      "処理": ["対象を1段目として、メソッド宣言までの祖先の制御構造を加算します。"],
      "引数": {"対象ノード": "段数を数える制御構造です。", "メソッド": "祖先の探索を止めるメソッドです。"},
      "戻り値": "対象を含めた制御構造の段数です。"
    }
    */
    private static int ネスト深度を数える(SyntaxNode 対象ノード, MethodDeclarationSyntax メソッド)
    {
        var 深さ = 1;
        foreach (var 祖先 in 対象ノード.Ancestors())
        {
            if (ReferenceEquals(祖先, メソッド))
            {
                break;
            }

            if (ネスト構造である(祖先))
            {
                深さ++;
            }
        }

        return 深さ;
    }

    /*
    {
      "責務": "OOP201の既存のネスト計測対象を選びます。",
      "処理": ["制御構造の構文型を照合します。"],
      "引数": {"対象ノード": "確認する構文です。"},
      "戻り値": "ネスト深度へ加える構文の場合にtrueです。"
    }
    */
    private static bool ネスト構造である(SyntaxNode 対象ノード) =>
        対象ノード
            is IfStatementSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or SwitchStatementSyntax
                or TryStatementSyntax
                or CatchClauseSyntax
                or LockStatementSyntax
                or UsingStatementSyntax;
}
