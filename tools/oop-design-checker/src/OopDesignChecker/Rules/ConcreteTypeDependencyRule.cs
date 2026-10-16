using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 具象型依存規則 : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP305", "Concrete dependency despite abstraction", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        var 利用側型使用状況群 = new Dictionary<ISymbol, 利用側型使用状況>(
            SymbolEqualityComparer.Default
        );

        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot(解析文脈.キャンセル通知);

            foreach (
                var コンストラクター in 構文根.DescendantNodes()
                    .OfType<ConstructorDeclarationSyntax>()
            )
            {
                foreach (var 引数 in コンストラクター.ParameterList.Parameters)
                {
                    if (
                        意味モデル.GetDeclaredSymbol(引数, 解析文脈.キャンセル通知)
                            is not IParameterSymbol 引数記号
                        || 引数記号.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class } 具象型
                    )
                    {
                        continue;
                    }

                    var 抽象候補群 = プロジェクト抽象判定器.意味のある抽象候補群を探す(具象型);
                    if (抽象候補群.Count == 0)
                    {
                        continue;
                    }

                    var 利用側型 = 引数記号.ContainingSymbol.ContainingType;
                    if (!利用側型使用状況群.TryGetValue(利用側型, out var 利用側型使用状況))
                    {
                        利用側型使用状況 = 利用側型の使用状況を作る(解析文脈, 利用側型);
                        利用側型使用状況群.Add(利用側型, 利用側型使用状況);
                    }

                    var 抽象 = 抽象候補群.FirstOrDefault(候補 =>
                        !抽象だけでは足りない契約を使う(
                            解析文脈,
                            引数記号,
                            具象型,
                            候補,
                            利用側型使用状況
                        )
                    );
                    if (抽象 is null)
                    {
                        continue;
                    }

                    yield return 診断生成器.生成する(
                        規則情報,
                        引数.Identifier.GetLocation(),
                        $"This constructor depends on concrete type {具象型.Name} even though project abstraction {抽象.Name} covers the behavior this consumer uses. Depend on the abstraction when replacement is part of the design.",
                        引数記号.ToDisplayString()
                    );
                }
            }
        }
    }

    private static bool 抽象だけでは足りない契約を使う(
        解析文脈 解析文脈,
        IParameterSymbol 引数,
        INamedTypeSymbol 具象型,
        INamedTypeSymbol 抽象,
        利用側型使用状況 利用側型使用状況
    )
    {
        var 追跡シンボル群 = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { 引数 };
        代入先メンバーを追跡する(引数, 利用側型使用状況, 追跡シンボル群);

        return 具象型だけのメンバーを使う(利用側型使用状況, 追跡シンボル群, 具象型, 抽象)
            || 具象型引数を必要とする(解析文脈, 利用側型使用状況, 追跡シンボル群, 抽象);
    }

    private static 利用側型使用状況 利用側型の使用状況を作る(
        解析文脈 解析文脈,
        INamedTypeSymbol 利用側型
    )
    {
        var 受け手別メンバー参照 = new Dictionary<ISymbol, List<ISymbol>>(
            SymbolEqualityComparer.Default
        );
        var シンボル別引数型 = new Dictionary<ISymbol, List<ITypeSymbol>>(
            SymbolEqualityComparer.Default
        );
        var 引数別代入先メンバー = new Dictionary<ISymbol, List<ISymbol>>(
            SymbolEqualityComparer.Default
        );

        foreach (var 構文参照 in 利用側型.DeclaringSyntaxReferences)
        {
            解析文脈.キャンセル通知.ThrowIfCancellationRequested();
            if (構文参照.GetSyntax(解析文脈.キャンセル通知) is not ClassDeclarationSyntax 宣言)
            {
                continue;
            }

            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(宣言.SyntaxTree);
            foreach (
                var メンバーアクセス in 宣言.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
            )
            {
                解析文脈.キャンセル通知.ThrowIfCancellationRequested();
                使用状況を追加する(
                    受け手別メンバー参照,
                    意味モデル
                        .GetSymbolInfo(メンバーアクセス.Expression, 解析文脈.キャンセル通知)
                        .Symbol,
                    意味モデル.GetSymbolInfo(メンバーアクセス, 解析文脈.キャンセル通知).Symbol
                );
            }

            foreach (var 引数構文 in 宣言.DescendantNodes().OfType<ArgumentSyntax>())
            {
                解析文脈.キャンセル通知.ThrowIfCancellationRequested();
                使用状況を追加する(
                    シンボル別引数型,
                    意味モデル.GetSymbolInfo(引数構文.Expression, 解析文脈.キャンセル通知).Symbol,
                    意味モデル
                        .GetTypeInfo(引数構文.Expression, 解析文脈.キャンセル通知)
                        .ConvertedType
                );
            }

            foreach (
                var コンストラクター in 宣言.DescendantNodes()
                    .OfType<ConstructorDeclarationSyntax>()
            )
            {
                解析文脈.キャンセル通知.ThrowIfCancellationRequested();
                if (
                    意味モデル.GetDeclaredSymbol(コンストラクター, 解析文脈.キャンセル通知)
                    is not IMethodSymbol コンストラクター記号
                )
                {
                    continue;
                }

                foreach (
                    var 代入 in コンストラクター
                        .DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                )
                {
                    var 値シンボル = 意味モデル
                        .GetSymbolInfo(代入.Right, 解析文脈.キャンセル通知)
                        .Symbol;
                    var 代入先シンボル = 意味モデル
                        .GetSymbolInfo(代入.Left, 解析文脈.キャンセル通知)
                        .Symbol;
                    if (
                        値シンボル is not null
                        && 代入先シンボル is IFieldSymbol or IPropertySymbol
                        && SymbolEqualityComparer.Default.Equals(
                            代入先シンボル.ContainingType,
                            コンストラクター記号.ContainingType
                        )
                    )
                    {
                        使用状況を追加する(引数別代入先メンバー, 値シンボル, 代入先シンボル);
                    }
                }
            }
        }

        return new 利用側型使用状況(受け手別メンバー参照, シンボル別引数型, 引数別代入先メンバー);
    }

    private static void 代入先メンバーを追跡する(
        IParameterSymbol 引数,
        利用側型使用状況 利用側型使用状況,
        HashSet<ISymbol> 追跡シンボル群
    )
    {
        if (!利用側型使用状況.引数別代入先メンバー.TryGetValue(引数, out var メンバー群))
        {
            return;
        }

        foreach (var メンバー in メンバー群)
        {
            追跡シンボル群.Add(メンバー);
        }
    }

    private static bool 具象型だけのメンバーを使う(
        利用側型使用状況 利用側型使用状況,
        HashSet<ISymbol> 追跡シンボル群,
        INamedTypeSymbol 具象型,
        INamedTypeSymbol 抽象
    )
    {
        foreach (var 追跡シンボル in 追跡シンボル群)
        {
            if (
                !利用側型使用状況.受け手別メンバー参照.TryGetValue(
                    追跡シンボル,
                    out var アクセス先メンバー群
                )
            )
            {
                continue;
            }

            foreach (var アクセス先メンバー in アクセス先メンバー群)
            {
                if (アクセス先メンバー.ContainingType?.SpecialType == SpecialType.System_Object)
                {
                    continue;
                }

                if (!抽象契約で提供される(具象型, 抽象, アクセス先メンバー))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool 具象型引数を必要とする(
        解析文脈 解析文脈,
        利用側型使用状況 利用側型使用状況,
        HashSet<ISymbol> 追跡シンボル群,
        INamedTypeSymbol 抽象
    )
    {
        foreach (var 追跡シンボル in 追跡シンボル群)
        {
            if (!利用側型使用状況.シンボル別引数型.TryGetValue(追跡シンボル, out var 変換先型群))
            {
                continue;
            }

            foreach (var 変換先型 in 変換先型群)
            {
                if (
                    !解析文脈
                        .対象プロジェクト.コンパイル結果.ClassifyConversion(抽象, 変換先型)
                        .IsImplicit
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool 抽象契約で提供される(
        INamedTypeSymbol 具象型,
        INamedTypeSymbol 抽象,
        ISymbol アクセス先メンバー
    )
    {
        foreach (var 契約型 in 抽象.AllInterfaces.Prepend(抽象))
        {
            foreach (var 契約メンバー in 契約型.GetMembers())
            {
                var 実装メンバー = 具象型.FindImplementationForInterfaceMember(契約メンバー);
                if (
                    SymbolEqualityComparer.Default.Equals(実装メンバー, アクセス先メンバー)
                    || SymbolEqualityComparer.Default.Equals(
                        実装メンバー?.OriginalDefinition,
                        アクセス先メンバー.OriginalDefinition
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void 使用状況を追加する<T>(
        Dictionary<ISymbol, List<T>> シンボル別使用状況,
        ISymbol? シンボル,
        T? 使用状況
    )
        where T : class
    {
        if (シンボル is null || 使用状況 is null)
        {
            return;
        }

        if (!シンボル別使用状況.TryGetValue(シンボル, out var 使用状況群))
        {
            使用状況群 = [];
            シンボル別使用状況.Add(シンボル, 使用状況群);
        }

        使用状況群.Add(使用状況);
    }

    private sealed record 利用側型使用状況(
        IReadOnlyDictionary<ISymbol, List<ISymbol>> 受け手別メンバー参照,
        IReadOnlyDictionary<ISymbol, List<ITypeSymbol>> シンボル別引数型,
        IReadOnlyDictionary<ISymbol, List<ISymbol>> 引数別代入先メンバー
    );
}
