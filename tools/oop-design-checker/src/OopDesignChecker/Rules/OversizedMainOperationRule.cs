using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class OversizedMainOperationRule : I解析規則
{
    private const int Minimum文数 = 20;
    private const int WarningScore = 4;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP201", "Oversized main operation", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol
                    || IsFrameworkManaged(methodSymbol)
                )
                {
                    continue;
                }

                var metrics = 処理量指標計算器.計算する(method, methodSymbol, semanticModel);
                if (
                    metrics.文数 < Minimum文数
                    || 過大スコアを計算する(metrics) < WarningScore
                    || IsPrivateHelper(context, methodSymbol)
                )
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    method.Identifier.GetLocation(),
                    $"This primary operation is structurally large: {metrics.行数} lines, {metrics.文数} statements, complexity {metrics.循環的複雑度}, nesting {metrics.最大ネスト深度}. Split meaningful processing phases into methods.",
                    methodSymbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsFrameworkManaged(IMethodSymbol method) =>
        FrameworkContractClassifier.HasExternalBaseTypeContract(method.ContainingType)
        || FrameworkContractClassifier.HasFrameworkContractAttribute(method.ContainingType)
        || FrameworkContractClassifier.HasFrameworkContractAttribute(method);

    private static int 過大スコアを計算する(処理量指標 metrics)
    {
        var score = 0;

        score += metrics.行数 switch
        {
            >= 120 => 2,
            >= 70 => 1,
            _ => 0,
        };
        score += metrics.循環的複雑度 switch
        {
            >= 18 => 2,
            >= 10 => 1,
            _ => 0,
        };
        score += metrics.最大ネスト深度 switch
        {
            >= 4 => 2,
            >= 3 => 1,
            _ => 0,
        };

        if (metrics.文数 >= 40)
        {
            score++;
        }

        if (metrics.ローカル変数数 >= 10)
        {
            score++;
        }

        if (metrics.状態メンバー数 >= 5)
        {
            score++;
        }

        if (metrics.トップレベル文数 >= 15)
        {
            score++;
        }

        return score;
    }

    private static bool IsPrivateHelper(解析文脈 context, IMethodSymbol target)
    {
        if (target.DeclaredAccessibility != Accessibility.Private)
        {
            return false;
        }

        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            foreach (
                var invocation in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
            )
            {
                if (
                    semanticModel.GetSymbolInfo(invocation).Symbol
                        is not IMethodSymbol invokedMethod
                    || !SymbolEqualityComparer.Default.Equals(
                        invokedMethod.OriginalDefinition,
                        target.OriginalDefinition
                    )
                )
                {
                    continue;
                }

                var callerDeclaration = invocation
                    .Ancestors()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();
                if (
                    callerDeclaration is not null
                    && semanticModel.GetDeclaredSymbol(callerDeclaration) is IMethodSymbol caller
                    && !SymbolEqualityComparer.Default.Equals(
                        caller.OriginalDefinition,
                        target.OriginalDefinition
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }
}
