using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 疑わしい継承関係規則 : I解析規則
{
    private const int 最小隠蔽メンバー数 = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP301", "Suspicious inheritance relationship", DesignDiagnosticSeverity.Warning);

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
                    || シンボル.BaseType
                        is not { SpecialType: not SpecialType.System_Object } 基底型
                    || !基底型.Locations.Any(location => location.IsInSource)
                )
                {
                    continue;
                }

                var 隠蔽メンバー群 = 宣言
                    .Members.Where(メンバー => メンバー.Modifiers.Any(SyntaxKind.NewKeyword))
                    .Select(メンバー => 意味モデル.GetDeclaredSymbol(メンバー))
                    .Where(メンバー =>
                        メンバー is not null && 対象となるインスタンス公開面である(メンバー)
                    )
                    .Where(メンバー => 同じ形のプロジェクトメンバーを隠す(メンバー!, 基底型))
                    .ToArray();
                if (隠蔽メンバー群.Length < 最小隠蔽メンバー数)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    宣言.Identifier.GetLocation(),
                    $"This child type replaces {隠蔽メンバー群.Length} inherited instance members from the project-owned {基底型.Name} hierarchy. Repeatedly replacing the parent-visible surface suggests the inheritance relationship may not model a stable is-a relationship.",
                    シンボル.ToDisplayString()
                );
            }
        }
    }

    private static bool 対象となるインスタンス公開面である(ISymbol メンバー) =>
        メンバー switch
        {
            IMethodSymbol メソッド => !メソッド.IsStatic
                && メソッド.MethodKind == MethodKind.Ordinary,
            IPropertySymbol property => !property.IsStatic,
            IEventSymbol @event => !@event.IsStatic,
            _ => false,
        };

    private static bool 同じ形のプロジェクトメンバーを隠す(
        ISymbol メンバー,
        INamedTypeSymbol 基底型
    )
    {
        for (
            var 現在の基底型 = 基底型;
            現在の基底型 is not null && 現在の基底型.Locations.Any(location => location.IsInSource);
            現在の基底型 = 現在の基底型.BaseType
        )
        {
            if (現在の基底型.GetMembers(メンバー.Name).Any(候補 => 同じ形である(メンバー, 候補)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool 同じ形である(ISymbol 子メンバー, ISymbol 親メンバー) =>
        (子メンバー, 親メンバー) switch
        {
            (IMethodSymbol child, IMethodSymbol parent) => !parent.IsStatic
                && parent.MethodKind == MethodKind.Ordinary
                && child.Arity == parent.Arity
                && 引数が一致する(child.Parameters, parent.Parameters),
            (IPropertySymbol child, IPropertySymbol parent) => !parent.IsStatic
                && child.IsIndexer == parent.IsIndexer
                && 引数が一致する(child.Parameters, parent.Parameters),
            (IEventSymbol, IEventSymbol parent) => !parent.IsStatic,
            _ => false,
        };

    private static bool 引数が一致する(
        ImmutableArray<IParameterSymbol> childParameters,
        ImmutableArray<IParameterSymbol> parentParameters
    )
    {
        if (childParameters.Length != parentParameters.Length)
        {
            return false;
        }

        for (var 引数位置 = 0; 引数位置 < childParameters.Length; 引数位置++)
        {
            if (
                childParameters[引数位置].RefKind != parentParameters[引数位置].RefKind
                || !SymbolEqualityComparer.Default.Equals(
                    childParameters[引数位置].Type,
                    parentParameters[引数位置].Type
                )
            )
            {
                return false;
            }
        }

        return true;
    }
}
