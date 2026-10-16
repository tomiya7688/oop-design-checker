using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 過剰オブジェクト辿り規則 : I解析規則
{
    private const int 最低辿り深度 = 3;

    public RuleDescriptor 規則情報 { get; } =
        new(
            "OOP404",
            "Excessive navigation through object internals",
            DesignDiagnosticSeverity.Attention
        );

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            foreach (var 呼び出し in 構文根.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (呼び出し.Expression is not MemberAccessExpressionSyntax メソッドアクセス)
                {
                    continue;
                }

                var 辿り深度 = プロジェクトオブジェクト境界数を数える(
                    メソッドアクセス.Expression,
                    意味モデル
                );
                if (辿り深度 < 最低辿り深度)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    呼び出し.GetLocation(),
                    $"This call traverses {辿り深度} project object boundaries before invoking behavior. Repeated deep navigation couples the caller to another object's internal graph."
                );
            }
        }
    }

    private static int プロジェクトオブジェクト境界数を数える(
        ExpressionSyntax 式,
        SemanticModel 意味モデル
    )
    {
        var 境界数 = 0;
        var 現在の要素 = 式;

        while (現在の要素 is MemberAccessExpressionSyntax メンバーアクセス)
        {
            var シンボル = 意味モデル.GetSymbolInfo(メンバーアクセス).Symbol;
            var メンバー型 = インスタンスメンバー型を読む(シンボル);
            var 所属型 = シンボル?.ContainingType;

            if (
                所属型 is not null
                && メンバー型 is not null
                && (
                    データ保持型判定器.明示的データ保持型である(所属型)
                    || データ保持型判定器.明示的データ保持型である(メンバー型)
                )
            )
            {
                return 0;
            }

            if (
                所属型 is not null
                && メンバー型 is not null
                && プロジェクトオブジェクト型である(所属型)
                && プロジェクトオブジェクト型である(メンバー型)
                && !同じオブジェクト抽象を表す(所属型, メンバー型)
            )
            {
                境界数++;
            }

            現在の要素 = メンバーアクセス.Expression;
        }

        return 境界数;
    }

    private static INamedTypeSymbol? インスタンスメンバー型を読む(ISymbol? シンボル) =>
        シンボル switch
        {
            IPropertySymbol { IsStatic: false, Type: INamedTypeSymbol 型 } => 型,
            IFieldSymbol { IsStatic: false, Type: INamedTypeSymbol 型 } => 型,
            _ => null,
        };

    private static bool プロジェクトオブジェクト型である(INamedTypeSymbol 型) =>
        型.IsReferenceType
        && 型.SpecialType == SpecialType.None
        && 型.Locations.Any(location => location.IsInSource);

    private static bool 同じオブジェクト抽象を表す(
        INamedTypeSymbol 所属型,
        INamedTypeSymbol メンバー型
    ) =>
        SymbolEqualityComparer.Default.Equals(所属型, メンバー型)
        || 実装または継承している(所属型, メンバー型)
        || 実装または継承している(メンバー型, 所属型);

    private static bool 実装または継承している(INamedTypeSymbol 型, INamedTypeSymbol 候補)
    {
        if (型.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, 候補)))
        {
            return true;
        }

        for (var 現在の要素 = 型.BaseType; 現在の要素 is not null; 現在の要素 = 現在の要素.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(現在の要素, 候補))
            {
                return true;
            }
        }

        return false;
    }
}
