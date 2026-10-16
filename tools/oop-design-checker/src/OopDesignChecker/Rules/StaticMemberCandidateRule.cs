using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class StaticMemberCandidateRule : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP103", "Static member candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            foreach (
                var declaration in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
            )
            {
                foreach (var diagnostic in クラスを解析する(declaration, semanticModel))
                {
                    yield return diagnostic;
                }
            }
        }
    }

    private IEnumerable<DesignDiagnostic> クラスを解析する(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        if (!IsEligibleClass(declaration, semanticModel))
        {
            yield break;
        }

        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodSymbol = GetStaticCandidate(method, declaration, semanticModel);
            if (methodSymbol is null)
            {
                continue;
            }

            yield return 診断生成器.生成する(
                規則情報,
                method.Identifier.GetLocation(),
                "This method does not use instance state and can be static.",
                methodSymbol.ToDisplayString()
            );
        }
    }

    private static bool IsEligibleClass(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        if (
            semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol classSymbol
            || HasUnresolvedBaseContract(declaration, semanticModel)
            || フレームワーク契約判定器.外部基底型契約を持つ(classSymbol)
            || フレームワーク契約判定器.フレームワーク契約属性を持つ(classSymbol)
        )
        {
            return false;
        }

        return !StaticEligibilityEvaluator.CanClassBeStatic(
            declaration,
            classSymbol,
            semanticModel
        );
    }

    private static IMethodSymbol? GetStaticCandidate(
        MethodDeclarationSyntax method,
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        if (
            semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol
            || methodSymbol.IsStatic
            || methodSymbol.IsAbstract
            || methodSymbol.IsVirtual
            || methodSymbol.IsOverride
            || method.ExplicitInterfaceSpecifier is not null
            || フレームワーク契約判定器.フレームワーク契約属性を持つ(methodSymbol)
            || シンボル操作器.インターフェースメンバーを実装している(
                methodSymbol,
                declaration,
                semanticModel
            )
            || インスタンス使用検査器.インスタンス状態を使う(method, methodSymbol, semanticModel)
        )
        {
            return null;
        }

        return methodSymbol;
    }

    private static bool HasUnresolvedBaseContract(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    ) =>
        declaration.BaseList?.Types.Any(baseType =>
            semanticModel.GetTypeInfo(baseType.Type).Type is null or { TypeKind: TypeKind.Error }
        ) == true;
}
