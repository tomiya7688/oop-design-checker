using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {"メソッド利用検証環境": "メモリ上のコンパイルと解析文脈を所有し、共有グラフと規則を同じソースで検証します。"},
  "フィールド": {"_文脈": "指定ソースだけを含む解析文脈です。", "グラフ": "Sampleの最初の宣言から構築した利用グラフです。", "対象型": "所属を比較するSample型の記号です。"}
}
*/
internal sealed class メソッド利用検証環境
{
    private readonly 解析文脈 _文脈;
    public 同一クラスメソッド利用グラフ グラフ { get; }
    public INamedTypeSymbol 対象型 { get; }

    /*
    {
      "責務": {"メソッド利用検証環境": "外部ファイルやMSBuildを使わず有効なC#ソースを解析できるようにします。"},
      "処理": ["メモリ上でコンパイルし、コンパイルエラーがないことを確認します。", "Sample宣言のグラフと単一プロジェクトの文脈を構成します。"],
      "引数": {"ソース": "Sampleクラスを含むC#コードです。"},
      "戻り値": []
    }
    */
    public メソッド利用検証環境(string ソース)
    {
        var 構文木 = CSharpSyntaxTree.ParseText(ソース, path: "Sample.cs");
        var コンパイル = CSharpCompilation.Create(
            "MethodUsageTests",
            [構文木],
            メタデータ参照提供器.プラットフォーム参照群を作る(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        Assert.DoesNotContain(
            コンパイル.GetDiagnostics(),
            診断 => 診断.Severity == DiagnosticSeverity.Error
        );
        var 意味モデル = コンパイル.GetSemanticModel(構文木);
        var 宣言 = 構文木.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(候補 => 候補.Identifier.ValueText == "Sample");
        対象型 = (INamedTypeSymbol)意味モデル.GetDeclaredSymbol(宣言)!;
        グラフ = new(対象型, 宣言, 意味モデル);
        var プロジェクト = new 解析対象プロジェクト(
            "virtual-root",
            false,
            コンパイル,
            new Dictionary<SyntaxTree, SemanticModel> { [構文木] = 意味モデル }
        );
        _文脈 = new(プロジェクト);
    }

    /*
    {
      "責務": {"規則を解析する": "指定した規則の診断を集約や抑制前に検証します。"},
      "処理": ["所有する解析文脈で規則を実行します。"],
      "引数": {"規則": "個別に検証する規則です。"},
      "戻り値": "生成された診断一覧です。"
    }
    */
    public DesignDiagnostic[] 規則を解析する(I解析規則 規則) => 規則.解析する(_文脈).ToArray();
}
