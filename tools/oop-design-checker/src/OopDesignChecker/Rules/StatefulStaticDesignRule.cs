using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 状態保持静的設計規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP105", "Stateful static design", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 宣言 in 構文根.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言)
                        is not INamedTypeSymbol { IsStatic: true } シンボル
                    || !シンボル操作器.主宣言である(シンボル, 宣言)
                )
                {
                    continue;
                }

                var 可変フィールド群 = シンボル.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(フィールド =>
                        フィールド.IsStatic
                        && !フィールド.IsConst
                        && !フィールド.IsImplicitlyDeclared
                        && !スレッドローカルである(フィールド)
                        && (
                            !フィールド.IsReadOnly
                            || 可変コレクション検査器.可変コレクションである(フィールド.Type)
                        )
                    )
                    .Cast<ISymbol>();

                var 可変プロパティ群 = シンボル.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(プロパティ =>
                        プロパティ.IsStatic
                        && !プロパティ.IsImplicitlyDeclared
                        && (
                            プロパティ.SetMethod is not null
                            || 可変コレクション検査器.可変コレクションである(プロパティ.Type)
                        )
                    )
                    .Cast<ISymbol>();

                var 可変メンバー群 = 可変フィールド群.Concat(可変プロパティ群).ToArray();
                if (可変メンバー群.Length == 0)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"This static class owns {可変メンバー群.Length} mutable shared member(s). Shared mutable state behaves like global state; prefer an object with an explicit lifetime.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }

    private static bool スレッドローカルである(IFieldSymbol フィールド) =>
        フィールド
            .GetAttributes()
            .Any(属性 => 属性.AttributeClass?.ToDisplayString() == "System.ThreadStaticAttribute");
}
