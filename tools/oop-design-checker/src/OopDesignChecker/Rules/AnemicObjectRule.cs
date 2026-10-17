using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

/*
{
  "責務": "複数の状態を使う更新処理が外部へ置かれたオブジェクトを検出します。",
  "フィールド": {"最小状態プロパティ数": "候補に必要な状態の数です。", "最小外部使用メンバー数": "外部処理が使う異なるメンバーの下限です。"}
}
*/
internal sealed class 貧血オブジェクト規則 : I解析規則
{
    private const int 最小状態プロパティ数 = 3;
    private const int 最小外部使用メンバー数 = 3;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP402", "Anemic object candidate", DesignDiagnosticSeverity.Warning);

    /*
    {
      "責務": "読み取り専用のデータ利用を除き、外部の状態更新を持つ候補を報告します。",
      "処理": ["状態更新の索引を作ります。", "候補型と更新する外部メソッドを照合します。"],
      "引数": {"文脈": "解析対象のプロジェクトです。"},
      "戻り値": "振る舞いの所有者を見直すための診断です。"
    }
    */
    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈)
    {
        var 更新索引 = new 状態更新利用索引(文脈);
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (var 宣言 in 構文ルート.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol 記号
                    || データ保持型判定器.データ保持属性がある(記号)
                    || !更新索引.状態更新がある(記号)
                    || 公開状態プロパティ数を数える(宣言) < 最小状態プロパティ数
                    || 公開振る舞いメソッド数を数える(宣言, 意味モデル) > 1
                )
                {
                    continue;
                }

                var 外部振る舞い = 外部の振る舞いを探す(文脈, 記号, 更新索引);
                if (外部振る舞い is null)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"{記号.Name} mostly exposes state while {外部振る舞い.Name} performs behavior by directly using several of its members. Consider moving state-owning behavior into the object.",
                    記号.ToDisplayString()
                );
            }
        }
    }

    private static int 公開状態プロパティ数を数える(ClassDeclarationSyntax 宣言) =>
        宣言
            .Members.OfType<PropertyDeclarationSyntax>()
            .Count(プロパティ =>
                プロパティ.Modifiers.Any(SyntaxKind.PublicKeyword)
                && プロパティ.AccessorList is not null
                && プロパティ.AccessorList.Accessors.All(アクセサ =>
                    アクセサ.Body is null && アクセサ.ExpressionBody is null
                )
            );

    private static int 公開振る舞いメソッド数を数える(
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    ) =>
        宣言
            .Members.OfType<MethodDeclarationSyntax>()
            .Count(メソッド =>
                意味モデル.GetDeclaredSymbol(メソッド)
                    is IMethodSymbol
                    {
                        IsStatic: false,
                        DeclaredAccessibility: Accessibility.Public,
                        MethodKind: MethodKind.Ordinary,
                    }
            );

    /*
    {
      "責務": "同じ引数の状態を更新し、複数のメンバーを使う外部処理を探します。",
      "処理": ["外部メソッドの引数と更新の有無を確認します。", "異なる使用メンバー数が下限を満たす処理を選びます。"],
      "引数": {"文脈": "検索するプロジェクトです。", "候補型": "状態の所有者です。", "更新索引": "型と引数ごとの状態更新です。"},
      "戻り値": "該当するメソッド、または見つからない場合にnullです。"
    }
    */
    private static IMethodSymbol? 外部の振る舞いを探す(
        解析文脈 文脈,
        INamedTypeSymbol 候補型,
        状態更新利用索引 更新索引
    )
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            foreach (
                var メソッド in 構文木.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            )
            {
                if (
                    意味モデル.GetDeclaredSymbol(メソッド) is not IMethodSymbol メソッド記号
                    || SymbolEqualityComparer.Default.Equals(メソッド記号.ContainingType, 候補型)
                    || 投影用基盤である(メソッド記号.ContainingType)
                )
                {
                    continue;
                }

                foreach (var 引数 in メソッド.ParameterList.Parameters)
                {
                    if (
                        意味モデル.GetDeclaredSymbol(引数) is not IParameterSymbol 引数記号
                        || !SymbolEqualityComparer.Default.Equals(引数記号.Type, 候補型)
                        || !更新索引.引数の状態を更新する(メソッド記号, 引数記号)
                    )
                    {
                        continue;
                    }

                    var メンバー群 = メソッド.DescendantNodes()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Where(access => 受信先が引数である(access, 引数記号, 意味モデル))
                        .Select(access => 意味モデル.GetSymbolInfo(access).Symbol)
                        .Where(メンバー =>
                            SymbolEqualityComparer.Default.Equals(メンバー?.ContainingType, 候補型)
                        )
                        .Select(メンバー => メンバー!.Name)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (メンバー群.Length >= 最小外部使用メンバー数)
                    {
                        return メソッド記号;
                    }
                }
            }
        }

        return null;
    }

    private static bool 受信先が引数である(
        MemberAccessExpressionSyntax access,
        IParameterSymbol 引数,
        SemanticModel 意味モデル
    ) =>
        SymbolEqualityComparer.Default.Equals(
            意味モデル.GetSymbolInfo(access.Expression).Symbol,
            引数
        );

    private static bool 投影用基盤である(INamedTypeSymbol 型)
    {
        var 型名 = 型.Name;
        return 型名.EndsWith("Mapper", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Formatter", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Serializer", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Converter", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Projection", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Presenter", StringComparison.OrdinalIgnoreCase)
            || 型名.EndsWith("Factory", StringComparison.OrdinalIgnoreCase);
    }
}
