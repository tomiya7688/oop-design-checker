using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 封止候補規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP102", "Sealing candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        if (!解析文脈.対象プロジェクト.アプリケーションである)
        {
            yield break;
        }

        var 型宣言情報群 = 解析文脈
            .対象プロジェクト.構文木群.SelectMany(tree =>
                tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            )
            .Select(宣言 => new 型宣言情報(
                宣言,
                解析文脈.対象プロジェクト.意味モデルを取得する(宣言.SyntaxTree)
                    .GetDeclaredSymbol(宣言) as INamedTypeSymbol
            ))
            .Where(型宣言情報 => 型宣言情報.シンボル is not null)
            .ToArray();

        var 継承先型群 = 型宣言情報群
            .Select(型宣言情報 => 型宣言情報.シンボル!.BaseType)
            .Where(基底型 => 基底型 is not null)
            .ToHashSet(SymbolEqualityComparer.Default);

        foreach (var 型宣言情報 in 型宣言情報群)
        {
            var シンボル = 型宣言情報.シンボル!;
            if (
                !シンボル操作器.主宣言である(シンボル, 型宣言情報.宣言)
                || シンボル.IsAbstract
                || シンボル.IsStatic
                || シンボル.IsSealed
                || シンボル.DeclaredAccessibility == Accessibility.Public
                || 継承先型群.Contains(シンボル)
                || 継承の意図を持つ(シンボル)
                || フレームワーク契約判定器.外部型契約を持つ(シンボル)
            )
            {
                continue;
            }

            yield return 診断生成器.生成する(
                規則情報,
                型宣言情報.宣言.Identifier.GetLocation(),
                "This application class has no known derived type or inheritance contract. Consider sealing it to make the design intent explicit.",
                シンボル.ToDisplayString()
            );
        }
    }

    private static bool 継承の意図を持つ(INamedTypeSymbol シンボル) =>
        シンボル.GetMembers()
            .Any(メンバー =>
                メンバー.DeclaredAccessibility
                    is Accessibility.Protected
                        or Accessibility.ProtectedOrInternal
                || メンバー is IMethodSymbol { IsVirtual: true }
            );

    private sealed record 型宣言情報(ClassDeclarationSyntax 宣言, INamedTypeSymbol? シンボル);
}
