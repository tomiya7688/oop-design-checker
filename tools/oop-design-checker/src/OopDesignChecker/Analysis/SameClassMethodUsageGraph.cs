using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": {"同一クラスメソッド利用グラフ": "ひとつのクラス宣言内の通常インスタンスメソッドについて、フィールド・プロパティ参照と同一型の呼出関係を保持し、到達先の利用を集計します。"},
  "フィールド": {"_メソッド利用群": "この宣言に含まれるメソッドの直接参照と呼出先です。"}
}
*/
internal sealed class 同一クラスメソッド利用グラフ
{
    private readonly Dictionary<IMethodSymbol, メソッド直接利用> _メソッド利用群;

    /*
    {
      "責務": {"同一クラスメソッド利用グラフ": "指定された宣言内のメソッドだけから利用グラフを構築します。"},
      "処理": ["通常インスタンスメソッドを選びます。", "識別子の参照先と同一型の通常インスタンス呼出先を収集します。"],
      "引数": {"包含型": "呼出先の所属を比較する型です。", "宣言": "解析するクラスの宣言です。別のpartial宣言は展開しません。", "意味モデル": "宣言を含む構文木の意味モデルです。"},
      "戻り値": []
    }
    */
    public 同一クラスメソッド利用グラフ(
        INamedTypeSymbol 包含型,
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    )
    {
        _メソッド利用群 = new(SymbolEqualityComparer.Default);
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

            // 両規則が使うメンバー参照だけを保持し、ローカル変数や型名を蓄積しません。
            var 参照先群 = メソッド宣言
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(識別子 => 意味モデル.GetSymbolInfo(識別子).Symbol)
                .OfType<ISymbol>()
                .Where(参照先 => 参照先 is IFieldSymbol or IPropertySymbol)
                .ToHashSet(SymbolEqualityComparer.Default);
            var 呼出先群 = 呼出先を集める(包含型, メソッド宣言, 意味モデル);
            _メソッド利用群[メソッド] = new(参照先群, 呼出先群);
        }
    }

    /*
    {
      "責務": {"有効利用を集計する": "規則が選んだ参照先について、helperを介して到達する利用も各メソッドへ集計します。"},
      "処理": ["型と選別条件に合う直接参照をメソッドごとに選びます。", "循環を除外しながら呼出先を辿り、空でない利用だけを返します。"],
      "型引数": {"Tメンバー": "規則が対象とする記号の型です。"},
      "引数": {"対象である": "状態メンバーや依存フィールドを選ぶ規則固有の条件です。"},
      "戻り値": "呼出先を含む利用一覧です。返した集合の変更は保持済みのグラフに影響しません。"
    }
    */
    public Dictionary<IMethodSymbol, HashSet<Tメンバー>> 有効利用を集計する<Tメンバー>(
        Func<Tメンバー, bool> 対象である
    )
        where Tメンバー : class, ISymbol
    {
        // 選別は探索の起点ごとに繰り返さず、各メソッドの直接参照へ一度適用します。
        var 直接利用群 = new Dictionary<IMethodSymbol, HashSet<Tメンバー>>(
            SymbolEqualityComparer.Default
        );
        foreach (var (メソッド, 利用) in _メソッド利用群)
        {
            直接利用群[メソッド] = new(
                利用.参照先群.OfType<Tメンバー>().Where(対象である),
                SymbolEqualityComparer.Default
            );
        }
        var 結果 = new Dictionary<IMethodSymbol, HashSet<Tメンバー>>(
            SymbolEqualityComparer.Default
        );
        foreach (var 起点 in _メソッド利用群.Keys)
        {
            var 有効利用群 = 到達先の利用を集める(起点, 直接利用群);
            if (有効利用群.Count > 0)
            {
                結果[起点] = 有効利用群;
            }
        }

        return 結果;
    }

    /*
    {
      "責務": {"到達先の利用を集める": "ひとつの起点から到達できるメソッドの対象参照を集めます。"},
      "処理": ["訪問済み記号で再帰と循環を打ち切ります。", "宣言内にある呼出先の利用を集合へ統合します。"],
      "型引数": {"Tメンバー": "集計する記号の型です。"},
      "引数": {"起点": "探索を開始するメソッドです。", "直接利用群": "規則固有の選別を済ませた直接参照です。"},
      "戻り値": "起点と到達先が利用する記号の集合です。"
    }
    */
    private HashSet<Tメンバー> 到達先の利用を集める<Tメンバー>(
        IMethodSymbol 起点,
        Dictionary<IMethodSymbol, HashSet<Tメンバー>> 直接利用群
    )
        where Tメンバー : class, ISymbol
    {
        var 結果 = new HashSet<Tメンバー>(SymbolEqualityComparer.Default);
        var 訪問済み群 = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var 待ち行列 = new Queue<IMethodSymbol>();
        待ち行列.Enqueue(起点);
        while (待ち行列.TryDequeue(out var 現在))
        {
            if (!訪問済み群.Add(現在) || !_メソッド利用群.TryGetValue(現在, out var 利用))
            {
                continue;
            }

            結果.UnionWith(直接利用群[現在]);
            foreach (var 呼出先 in 利用.呼出先群)
            {
                待ち行列.Enqueue(呼出先);
            }
        }

        return 結果;
    }

    /*
    {
      "責務": {"呼出先を集める": "同じ型に属する通常インスタンスメソッドへの呼出式を解決します。"},
      "処理": ["呼出式の記号を解決し、static・別型・通常メソッド以外を除外します。"],
      "引数": {"包含型": "呼出先の所属を比較する型です。", "宣言": "呼出元のメソッド宣言です。", "意味モデル": "呼出式を解決する意味モデルです。"},
      "戻り値": "解決された記号をそのまま保持した呼出先集合です。受信側オブジェクトによる限定やOriginalDefinitionへの変換は行いません。"
    }
    */
    private static HashSet<IMethodSymbol> 呼出先を集める(
        INamedTypeSymbol 包含型,
        MethodDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    )
    {
        var 結果 = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        foreach (var 呼出式 in 宣言.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (
                意味モデル.GetSymbolInfo(呼出式).Symbol is IMethodSymbol 呼出先
                && !呼出先.IsStatic
                && 呼出先.MethodKind == MethodKind.Ordinary
                && SymbolEqualityComparer.Default.Equals(呼出先.ContainingType, 包含型)
            )
            {
                結果.Add(呼出先);
            }
        }

        return 結果;
    }

    /*
    {
      "責務": {"メソッド直接利用": "グラフ内部の直接参照と呼出先をひとつのメソッドに対応付けます。"},
      "フィールド": {"参照先群": "識別子から解決した記号です。", "呼出先群": "同じ型の通常インスタンスメソッドです。"}
    }
    */
    private sealed record メソッド直接利用(
        IReadOnlySet<ISymbol> 参照先群,
        IReadOnlySet<IMethodSymbol> 呼出先群
    );
}
