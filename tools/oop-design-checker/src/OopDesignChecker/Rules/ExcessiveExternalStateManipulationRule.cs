using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class 外部状態操作過剰規則 : I解析規則
{
    private const int 最小異なるメンバー数 = 3;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP108", "Excessive external state manipulation", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈)
    {
        foreach (var 構文木 in 文脈.対象プロジェクト.構文木群)
        {
            var 意味モデル = 文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文ルート = 構文木.GetRoot();

            foreach (
                var メソッド宣言 in 構文ルート.DescendantNodes().OfType<MethodDeclarationSyntax>()
            )
            {
                if (意味モデル.GetDeclaredSymbol(メソッド宣言) is not IMethodSymbol メソッド)
                {
                    continue;
                }

                var 操作対象群 = メソッド宣言
                    .DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Select(代入式 => 外部書き込みを読み取る(代入式, メソッド, 意味モデル))
                    .Where(書き込み => 書き込み is not null)
                    .Select(書き込み => 書き込み!.Value)
                    .GroupBy(
                        書き込み => (書き込み.受信対象, 書き込み.対象型),
                        外部書き込み識別子比較器.Instance
                    );

                foreach (var まとまり in 操作対象群)
                {
                    var 重複のないメンバー群 = まとまり.Select(書き込み => 書き込み.メンバー名)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (重複のないメンバー群.Length < 最小異なるメンバー数)
                    {
                        continue;
                    }

                    yield return 診断生成器.生成する(
                        規則情報,
                        メソッド宣言.Identifier.GetLocation(),
                        $"This method directly writes {重複のないメンバー群.Length} メンバー群 of {まとまり.Key.対象型.Name} through {まとまり.Key.受信対象}. Prefer asking that object to perform the state transition itself.",
                        メソッド.ToDisplayString()
                    );
                }
            }
        }
    }

    private static 外部書き込み? 外部書き込みを読み取る(
        AssignmentExpressionSyntax 代入式,
        IMethodSymbol メソッド,
        SemanticModel 意味モデル
    )
    {
        if (代入式.Left is not MemberAccessExpressionSyntax メンバーアクセス構文)
        {
            return null;
        }

        var メンバー = 意味モデル.GetSymbolInfo(メンバーアクセス構文).Symbol;
        var 対象型 = メンバー?.ContainingType;
        if (
            対象型 is null
            || !対象型.Locations.Any(location => location.IsInSource)
            || SymbolEqualityComparer.Default.Equals(対象型, メソッド.ContainingType)
            || データ保持型判定器.明示的データ保持型である(対象型)
            || !公開インスタンス状態である(メンバー)
        )
        {
            return null;
        }

        return new 外部書き込み(メンバーアクセス構文.Expression.ToString(), 対象型, メンバー!.Name);
    }

    private static bool 公開インスタンス状態である(ISymbol? 記号) =>
        記号
            is IPropertySymbol
                {
                    IsStatic: false,
                    SetMethod.DeclaredAccessibility: Accessibility.Public
                }
                or IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public };

    private readonly record struct 外部書き込み(
        string 受信対象,
        INamedTypeSymbol 対象型,
        string メンバー名
    );

    private sealed class 外部書き込み識別子比較器
        : IEqualityComparer<(string 受信対象, INamedTypeSymbol 対象型)>
    {
        public static 外部書き込み識別子比較器 Instance { get; } = new();

        public bool Equals(
            (string 受信対象, INamedTypeSymbol 対象型) left,
            (string 受信対象, INamedTypeSymbol 対象型) right
        ) =>
            StringComparer.Ordinal.Equals(left.受信対象, right.受信対象)
            && SymbolEqualityComparer.Default.Equals(left.対象型, right.対象型);

        public int GetHashCode((string 受信対象, INamedTypeSymbol 対象型) value) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.受信対象),
                SymbolEqualityComparer.Default.GetHashCode(value.対象型)
            );
    }
}
