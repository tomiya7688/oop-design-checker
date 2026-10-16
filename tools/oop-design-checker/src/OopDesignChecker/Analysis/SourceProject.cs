using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OopDesignChecker.Analysis;

internal sealed class 解析対象プロジェクト
{
    private readonly IReadOnlyDictionary<SyntaxTree, SemanticModel> _意味モデル群;
    private readonly IReadOnlyList<SyntaxTree> _構文木群;

    public 解析対象プロジェクト(
        string ルートパス,
        bool アプリケーションである,
        CSharpCompilation コンパイル結果,
        IReadOnlyDictionary<SyntaxTree, SemanticModel> 意味モデル群,
        IReadOnlyList<SyntaxTree>? 構文木群 = null
    )
    {
        this.ルートパス = ルートパス;
        this.アプリケーションである = アプリケーションである;
        this.コンパイル結果 = コンパイル結果;
        _意味モデル群 = 意味モデル群;
        _構文木群 = 構文木群 ?? コンパイル結果.SyntaxTrees.ToArray();
    }

    public string ルートパス { get; }

    public bool アプリケーションである { get; }

    public CSharpCompilation コンパイル結果 { get; }

    public IEnumerable<SyntaxTree> 構文木群 => _構文木群;

    public SemanticModel 意味モデルを取得する(SyntaxTree 構文木) => _意味モデル群[構文木];
}
