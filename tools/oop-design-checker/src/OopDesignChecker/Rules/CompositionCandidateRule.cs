using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class コンポジション候補規則 : I解析規則
{
    private const int 最低再利用保護メンバー数 = 2;

    public RuleDescriptor 規則情報 { get; } =
        new(
            "OOP307",
            "Composition may be more appropriate than inheritance",
            DesignDiagnosticSeverity.Attention
        );

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 宣言 in 構文根.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    意味モデル.GetDeclaredSymbol(宣言) is not INamedTypeSymbol シンボル
                    || !シンボル操作器.主宣言である(シンボル, 宣言)
                    || シンボル.BaseType is not { } 基底型
                    || 基底型.SpecialType == SpecialType.System_Object
                    || 基底型.IsAbstract
                    || !基底型.Locations.Any(location => location.IsInSource)
                    || 振る舞いを特殊化している(宣言, 意味モデル)
                )
                {
                    continue;
                }

                var 再利用処理 = 宣言
                    .Members.OfType<MethodDeclarationSyntax>()
                    .Select(メソッド => 保護メンバー再利用を読む(メソッド, 意味モデル, 基底型))
                    .Where(reuse => reuse is not null)
                    .OrderByDescending(reuse => reuse!.使用保護メンバー数)
                    .FirstOrDefault();
                if (再利用処理 is null)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"Child operation {再利用処理.処理名} builds new behavior from {再利用処理.使用保護メンバー数} protected members of concrete base {基底型.Name} without specializing parent behavior. Composition may express the relationship more accurately.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }

    private static bool 振る舞いを特殊化している(
        ClassDeclarationSyntax 宣言,
        SemanticModel 意味モデル
    ) =>
        宣言
            .Members.OfType<MethodDeclarationSyntax>()
            .Any(メソッド =>
                意味モデル.GetDeclaredSymbol(メソッド) is IMethodSymbol { IsOverride: true }
            );

    private static 保護メンバー再利用? 保護メンバー再利用を読む(
        MethodDeclarationSyntax 宣言,
        SemanticModel 意味モデル,
        INamedTypeSymbol 基底型
    )
    {
        if (
            意味モデル.GetDeclaredSymbol(宣言)
            is not IMethodSymbol
            {
                IsStatic: false,
                MethodKind: MethodKind.Ordinary,
                DeclaredAccessibility: not Accessibility.Private,
            } メソッド
        )
        {
            return null;
        }

        var 使用保護メンバー数 = 宣言.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Select(name => 意味モデル.GetSymbolInfo(name).Symbol)
            .Where(メンバー => 基底型の保護メンバーである(メンバー, 基底型))
            .Select(メンバー => メンバー!)
            .Distinct(SymbolEqualityComparer.Default)
            .Count();
        if (使用保護メンバー数 < 最低再利用保護メンバー数)
        {
            return null;
        }

        return new 保護メンバー再利用(メソッド.Name, 使用保護メンバー数);
    }

    private static bool 基底型の保護メンバーである(ISymbol? メンバー, INamedTypeSymbol 基底型) =>
        メンバー is not null
        && インスタンスメンバーである(メンバー)
        && メンバー.DeclaredAccessibility
            is Accessibility.Protected
                or Accessibility.ProtectedOrInternal
                or Accessibility.ProtectedAndInternal
        && メンバー.ContainingType is not null
        && シンボル操作器.同じ型か基底型である(メンバー.ContainingType, 基底型);

    private static bool インスタンスメンバーである(ISymbol メンバー) =>
        メンバー switch
        {
            IMethodSymbol メソッド => !メソッド.IsStatic,
            IPropertySymbol プロパティ => !プロパティ.IsStatic,
            IFieldSymbol フィールド => !フィールド.IsStatic,
            IEventSymbol イベント => !イベント.IsStatic,
            _ => false,
        };

    private sealed record 保護メンバー再利用(string 処理名, int 使用保護メンバー数);
}
