using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

/*
{
  "責務": {
    "AvoidableConcreteConstructionRule": "所有範囲外で置換可能な具象型を生成する箇所を検出する"
  },
  "フィールド": {
    "CompositionTypeSuffixes": "composition rootとして扱う型名の末尾",
    "CompositionMethodPrefixes": "composition処理として扱うメソッド名の先頭",
    "Descriptor": "OOP306の識別子、説明、severity"
  },
  "処理": [
    "各構文木の生成式を調べ、所有constructionを除外する",
    "意味のある抽象契約を持つ型に対し、composition境界外の生成を警告する"
  ]
}
*/
internal sealed class AvoidableConcreteConstructionRule : I解析規則
{
    // Startupや起動処理を表す一般的な型名をcomposition境界として扱う。
    private static readonly string[] CompositionTypeSuffixes =
    [
        "CompositionRoot",
        "Bootstrap",
        "Bootstrapper",
        "Startup",
        "HostBuilder",
        "AppHost",
    ];

    // Composition境界の型で、配線処理を表す一般的なメソッド名を受け入れる。
    private static readonly string[] CompositionMethodPrefixes =
    [
        "Build",
        "Compose",
        "Configure",
        "Create",
        "Initialize",
        "Register",
        "Setup",
        "Wire",
    ];

    public RuleDescriptor 規則情報 { get; } =
        new(
            "OOP306",
            "Avoidable concrete construction dependency",
            DesignDiagnosticSeverity.Warning
        );

    /*
    {
      "責務": {
        "解析する": "所有範囲外にある置換可能な具象型生成を診断する"
      },
      "処理": [
        "解析対象の構文木から具象型の生成式を取得する",
        "所有construction、抽象契約のない型、振る舞いのない型を除外する",
        "生成位置の型を特定し、同じ型による生成を除外する",
        "残った生成式にOOP306診断を作成する"
      ],
      "引数": {
        "context": "解析対象project、コンパイル情報、キャンセル状態"
      },
      "戻り値": "検出したOOP306診断の列挙"
    }
    */
    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            // 構文木に対応する意味モデルを使い、生成式の型と所属を解決する。
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            // new式ごとに具象型依存の候補かを判定する。
            foreach (
                var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            )
            {
                if (
                    semanticModel.GetTypeInfo(creation).Type is not INamedTypeSymbol concreteType
                    || concreteType.TypeKind != TypeKind.Class
                    || IsOwnedConstruction(creation, semanticModel)
                )
                {
                    continue;
                }

                var abstraction = プロジェクト抽象判定器.意味のある抽象候補を探す(concreteType);
                if (abstraction is null || !HasBehavior(concreteType))
                {
                    continue;
                }

                var containingType = semanticModel
                    .GetEnclosingSymbol(creation.SpanStart)
                    ?.ContainingType;
                if (
                    containingType is null
                    || SymbolEqualityComparer.Default.Equals(containingType, concreteType)
                )
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    creation.NewKeyword.GetLocation(),
                    $"{containingType.Name} directly constructs replaceable collaborator {concreteType.Name}, while project abstraction {abstraction.Name} defines a behavioral contract. Prefer receiving the abstraction when the collaborator lifecycle is not owned here.",
                    containingType.ToDisplayString()
                );
            }
        }
    }

    /*
    {
      "責務": {
        "HasBehavior": "型が宣言したinstanceの通常メソッドを持つか判定する"
      },
      "処理": [
        "型の宣言memberからinstanceの通常メソッドを探す"
      ],
      "引数": {
        "type": "振る舞いの有無を確認する型"
      },
      "戻り値": "instanceの通常メソッドがあればtrue"
    }
    */
    private static bool HasBehavior(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic);

    /*
    {
      "責務": {
        "IsOwnedConstruction": "生成側がlifecycleを所有するconstructionを判定する"
      },
      "処理": [
        "返り値、nested object graph、collection expression、composition rootの配線を確認する"
      ],
      "引数": {
        "creation": "所有範囲を判定する生成式",
        "semanticModel": "local symbolと所属methodを解決する意味モデル"
      },
      "戻り値": "いずれかの所有条件を満たす場合はtrue"
    }
    */
    private static bool IsOwnedConstruction(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel
    ) =>
        IsDirectReturn(creation)
        || IsNestedObjectGraphConstruction(creation)
        || creation.Ancestors().OfType<CollectionExpressionSyntax>().Any()
        || IsCompositionRootLocalWiring(creation, semanticModel);

    /*
    {
      "責務": {
        "IsDirectReturn": "生成式がmethodまたはpropertyの返り値になっているか判定する"
      },
      "処理": [
        "括弧、cast、switch式、conditional式の結果部分をたどる",
        "最後にreturn文またはexpression bodyへ直接つながるか確認する"
      ],
      "引数": {
        "creation": "返り値との関係を調べる生成式"
      },
      "戻り値": "生成式が直接返される場合はtrue"
    }
    */
    private static bool IsDirectReturn(ObjectCreationExpressionSyntax creation)
    {
        SyntaxNode expression = creation;

        while (true)
        {
            // 括弧とcastは返り値との直接性を変えないため、その外側を調べ続ける。
            if (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            {
                expression = expression.Parent;
                continue;
            }

            // Switch armやconditionalのbranchが返り値なら、その包含式をたどる。
            if (expression.Parent is SwitchExpressionArmSyntax arm)
            {
                expression = arm;
                continue;
            }

            if (
                expression is SwitchExpressionArmSyntax
                && expression.Parent is SwitchExpressionSyntax switchExpression
            )
            {
                expression = switchExpression;
                continue;
            }

            if (
                expression.Parent is ConditionalExpressionSyntax conditional
                && (conditional.WhenTrue == expression || conditional.WhenFalse == expression)
            )
            {
                expression = conditional;
                continue;
            }

            return expression.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax;
        }
    }

    /*
    {
      "責務": {
        "IsNestedObjectGraphConstruction": "別のobject creationへ直接渡す生成式を判定する"
      },
      "処理": [
        "生成式を含む最も近いargumentを探す",
        "そのargumentを含むobject creationがあればnested constructionとする"
      ],
      "引数": {
        "creation": "nested constructionかを調べる生成式"
      },
      "戻り値": "別のobject creationのargument内にある場合はtrue"
    }
    */
    private static bool IsNestedObjectGraphConstruction(ObjectCreationExpressionSyntax creation)
    {
        var argument = creation.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
        return argument?.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any() == true;
    }

    /*
    {
      "責務": {
        "IsCompositionRootLocalWiring": "composition contextで生成したlocalがconstructor注入だけに使われるか判定する"
      },
      "処理": [
        "括弧やcastの外側にあるlocal initializerを確認する",
        "local、所属method、composition contextを特定する",
        "method内のlocal参照を集める",
        "参照がすべてobject creationのconstructor argumentなら配線として扱う"
      ],
      "引数": {
        "creation": "local initializerにある生成式",
        "semanticModel": "localとmethodの所属を解決する意味モデル"
      },
      "戻り値": "composition contextで生成され、全参照がconstructor argumentならtrue"
    }
    */
    private static bool IsCompositionRootLocalWiring(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel
    )
    {
        // 括弧とcastの違いを無視して、local initializerかを判定する。
        SyntaxNode expression = creation;
        while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            expression = expression.Parent;
        }

        if (
            expression.Parent
                is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            || semanticModel.GetDeclaredSymbol(variable) is not ILocalSymbol local
            || semanticModel.GetEnclosingSymbol(creation.SpanStart) is not IMethodSymbol method
            || !IsCompositionContext(method)
        )
        {
            return false;
        }

        // localの利用先を一つ残らず調べ、業務処理へ流出するlocalを誤って除外しない。
        var scope = variable.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (scope is null)
        {
            return false;
        }

        var references = scope
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier).Symbol,
                    local
                )
            )
            .ToArray();

        return references.Length > 0 && references.All(IsDirectConstructorArgument);
    }

    /*
    {
      "責務": {
        "IsDirectConstructorArgument": "local参照がobject creationのargumentとして使われるか判定する"
      },
      "処理": [
        "括弧とcastを越えて参照の親を確認する",
        "参照がargument内にあり、argumentがobject creationに属するか調べる"
      ],
      "引数": {
        "reference": "利用先を確認するlocal参照"
      },
      "戻り値": "object creationのargument内にある場合はtrue"
    }
    */
    private static bool IsDirectConstructorArgument(IdentifierNameSyntax reference)
    {
        // 括弧やcastで包まれていてもconstructor argumentとして扱う。
        SyntaxNode expression = reference;
        while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            expression = expression.Parent;
        }

        return expression.Parent is ArgumentSyntax argument
            && argument.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any();
    }

    /*
    {
      "責務": {
        "IsCompositionContext": "methodがアプリケーションのcomposition境界に属するか判定する"
      },
      "処理": [
        "static Mainをentry pointとして扱う",
        "型名の慣例とconstructorまたは配線用method名を組み合わせて確認する"
      ],
      "引数": {
        "method": "composition境界かを判定するmethod"
      },
      "戻り値": "entry pointまたは慣例に合うcomposition methodならtrue"
    }
    */
    private static bool IsCompositionContext(IMethodSymbol method)
    {
        // Mainは型名に依存しないapplication entry pointとして扱う。
        if (method.IsStatic && method.Name == "Main")
        {
            return true;
        }

        var containingTypeName = method.ContainingType?.Name;
        if (containingTypeName is null || !IsCompositionTypeName(containingTypeName))
        {
            return false;
        }

        // Composition rootのconstructorは依存関係を組み立てる場所として扱う。
        if (method.MethodKind == MethodKind.Constructor)
        {
            return true;
        }

        return CompositionMethodPrefixes.Any(prefix =>
            method.Name.StartsWith(prefix, StringComparison.Ordinal)
        );
    }

    /*
    {
      "責務": {
        "IsCompositionTypeName": "型名がcomposition rootの慣例に合うか判定する"
      },
      "処理": [
        "Programとの完全一致または登録済みsuffixとの一致を調べる"
      ],
      "引数": {
        "typeName": "判定対象の型名"
      },
      "戻り値": "composition rootの慣例に合う場合はtrue"
    }
    */
    private static bool IsCompositionTypeName(string typeName) =>
        typeName == "Program"
        || CompositionTypeSuffixes.Any(suffix =>
            typeName.EndsWith(suffix, StringComparison.Ordinal)
        );
}
