using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

/*
{
  "責務": {
    "回避可能な具象生成規則": "所有範囲外で置換可能な具象型を生成する箇所を検出する"
  },
  "フィールド": {
    "構成型名の末尾群": "composition rootとして扱う型名の末尾",
    "構成メソッド名の接頭辞群": "composition処理として扱うメソッド名の先頭",
    "規則情報": "OOP306の識別子、説明、重要度"
  },
  "処理": [
    "各構文木の生成式を調べ、所有constructionを除外する",
    "意味のある抽象契約を持つ型に対し、composition境界外の生成を警告する"
  ]
}
*/
internal sealed class 回避可能な具象生成規則 : I解析規則
{
    // Startupや起動処理を表す一般的な型名を構成境界として扱う。
    private static readonly string[] 構成型名の末尾群 =
    [
        "CompositionRoot",
        "Bootstrap",
        "Bootstrapper",
        "Startup",
        "HostBuilder",
        "AppHost",
    ];

    // 構成境界の型で、配線処理を表す一般的なメソッド名を受け入れる。
    private static readonly string[] 構成メソッド名の接頭辞群 =
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
    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 解析文脈)
    {
        foreach (var 構文木 in 解析文脈.対象プロジェクト.構文木群)
        {
            // 構文木に対応する意味モデルを使い、生成式の型と所属を解決する。
            var 意味モデル = 解析文脈.対象プロジェクト.意味モデルを取得する(構文木);
            var 構文根 = 構文木.GetRoot();

            // new式ごとに具象型依存の候補かを判定する。
            foreach (
                var 生成式 in 構文根.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            )
            {
                if (
                    意味モデル.GetTypeInfo(生成式).Type is not INamedTypeSymbol 具象型
                    || 具象型.TypeKind != TypeKind.Class
                    || 所有範囲内の生成である(生成式, 意味モデル)
                )
                {
                    continue;
                }

                var 抽象 = プロジェクト抽象判定器.意味のある抽象候補を探す(具象型);
                if (抽象 is null || !振る舞いを持つ(具象型))
                {
                    continue;
                }

                var 所属型 = 意味モデル.GetEnclosingSymbol(生成式.SpanStart)?.ContainingType;
                if (所属型 is null || SymbolEqualityComparer.Default.Equals(所属型, 具象型))
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    生成式.NewKeyword.GetLocation(),
                    $"{所属型.Name} directly constructs replaceable collaborator {具象型.Name}, while project abstraction {抽象.Name} defines a behavioral contract. Prefer receiving the abstraction when the collaborator lifecycle is not owned here.",
                    所属型.ToDisplayString()
                );
            }
        }
    }

    /*
    {
      "責務": {
        "振る舞いを持つ": "型が宣言したinstanceの通常メソッドを持つか判定する"
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
    private static bool 振る舞いを持つ(INamedTypeSymbol 型) =>
        型.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(メソッド => メソッド.MethodKind == MethodKind.Ordinary && !メソッド.IsStatic);

    /*
    {
      "責務": {
        "所有範囲内の生成である": "生成側がlifecycleを所有するconstructionを判定する"
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
    private static bool 所有範囲内の生成である(
        ObjectCreationExpressionSyntax 生成式,
        SemanticModel 意味モデル
    ) =>
        直接返される生成である(生成式)
        || 入れ子オブジェクト生成である(生成式)
        || 生成式.Ancestors().OfType<CollectionExpressionSyntax>().Any()
        || 構成ルートのローカル配線である(生成式, 意味モデル);

    /*
    {
      "責務": {
        "直接返される生成である": "生成式がmethodまたはpropertyの返り値になっているか判定する"
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
    private static bool 直接返される生成である(ObjectCreationExpressionSyntax 生成式)
    {
        SyntaxNode 式 = 生成式;

        while (true)
        {
            // 括弧とcastは返り値との直接性を変えないため、その外側を調べ続ける。
            if (式.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            {
                式 = 式.Parent;
                continue;
            }

            // Switch armやconditionalのbranchが返り値なら、その包含式をたどる。
            if (式.Parent is SwitchExpressionArmSyntax 分岐)
            {
                式 = 分岐;
                continue;
            }

            if (式 is SwitchExpressionArmSyntax && 式.Parent is SwitchExpressionSyntax switch式)
            {
                式 = switch式;
                continue;
            }

            if (
                式.Parent is ConditionalExpressionSyntax 条件式
                && (条件式.WhenTrue == 式 || 条件式.WhenFalse == 式)
            )
            {
                式 = 条件式;
                continue;
            }

            return 式.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax;
        }
    }

    /*
    {
      "責務": {
        "入れ子オブジェクト生成である": "別のobject creationへ直接渡す生成式を判定する"
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
    private static bool 入れ子オブジェクト生成である(ObjectCreationExpressionSyntax 生成式)
    {
        var 引数構文 = 生成式.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
        return 引数構文?.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any() == true;
    }

    /*
    {
      "責務": {
        "構成ルートのローカル配線である": "composition contextで生成したlocalがconstructor注入だけに使われるか判定する"
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
    private static bool 構成ルートのローカル配線である(
        ObjectCreationExpressionSyntax 生成式,
        SemanticModel 意味モデル
    )
    {
        // 括弧とcastの違いを無視して、local initializerかを判定する。
        SyntaxNode 式 = 生成式;
        while (式.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            式 = 式.Parent;
        }

        if (
            式.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax 変数宣言 }
            || 意味モデル.GetDeclaredSymbol(変数宣言) is not ILocalSymbol ローカル変数
            || 意味モデル.GetEnclosingSymbol(生成式.SpanStart) is not IMethodSymbol メソッド
            || !構成境界に属する(メソッド)
        )
        {
            return false;
        }

        // localの利用先を一つ残らず調べ、業務処理へ流出するlocalを誤って除外しない。
        var 探索範囲 = 変数宣言.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (探索範囲 is null)
        {
            return false;
        }

        var 参照群 = 探索範囲.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    意味モデル.GetSymbolInfo(identifier).Symbol,
                    ローカル変数
                )
            )
            .ToArray();

        return 参照群.Length > 0 && 参照群.All(コンストラクター引数である);
    }

    /*
    {
      "責務": {
        "コンストラクター引数である": "local参照がobject creationのargumentとして使われるか判定する"
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
    private static bool コンストラクター引数である(IdentifierNameSyntax reference)
    {
        // 括弧やcastで包まれていてもconstructor argumentとして扱う。
        SyntaxNode 式 = reference;
        while (式.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            式 = 式.Parent;
        }

        return 式.Parent is ArgumentSyntax 引数構文
            && 引数構文.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any();
    }

    /*
    {
      "責務": {
        "構成境界に属する": "methodがアプリケーションのcomposition境界に属するか判定する"
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
    private static bool 構成境界に属する(IMethodSymbol メソッド)
    {
        // Mainは型名に依存しないapplication entry pointとして扱う。
        if (メソッド.IsStatic && メソッド.Name == "Main")
        {
            return true;
        }

        var 所属型名 = メソッド.ContainingType?.Name;
        if (所属型名 is null || !構成型名である(所属型名))
        {
            return false;
        }

        // Composition rootのconstructorは依存関係を組み立てる場所として扱う。
        if (メソッド.MethodKind == MethodKind.Constructor)
        {
            return true;
        }

        return 構成メソッド名の接頭辞群.Any(接頭辞 =>
            メソッド.Name.StartsWith(接頭辞, StringComparison.Ordinal)
        );
    }

    /*
    {
      "責務": {
        "構成型名である": "型名がcomposition rootの慣例に合うか判定する"
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
    private static bool 構成型名である(string 型名) =>
        型名 == "Program"
        || 構成型名の末尾群.Any(末尾 => 型名.EndsWith(末尾, StringComparison.Ordinal));
}
