# チェックルール — 1.0.0 正式仕様

[English](check-rules.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

> 対象version: **1.0.0**。この文書は1.0.0で実装されるC# ruleの正本です。

この文書は、1.0.0で実装・release gateされるチェック内容とseverity、既知の除外境界、設定契約を定義します。概念上の将来候補は「実装済みrule」として扱いません。

## 1.0.0 rule catalog

1.0.0で実装されるrule IDとseverity契約は次のとおりです。ここでのseverityはruleの既定または固定severityです。

| Rule | 意味 | Severity |
| --- | --- | --- |
| OOP001 | 共通抽象の欠如 | WARNING |
| OOP002 | 型分岐による多態性の迂回 | WARNING |
| OOP003 | 不要な抽象 | ATTENTION |
| OOP101 | 過剰な可視性 | WARNING |
| OOP102 | sealed候補 | ATTENTION |
| OOP103 | static member候補 | ATTENTION |
| OOP104 | static class候補 | ATTENTION |
| OOP105 | stateful static設計 | WARNING |
| OOP106 | カプセル化漏れ | WARNING（高確度の直接変更可能な公開はDANGER） |
| OOP107 | オブジェクト不変条件の迂回 | DANGER |
| OOP108 | 過剰な外部状態操作 | WARNING |
| OOP201 | 巨大な主要操作 | WARNING |
| OOP301 | 疑わしい継承関係 | WARNING |
| OOP302 | 親契約の大部分を未使用 | WARNING |
| OOP303 | 子が親の振る舞いを無効化 | DANGER |
| OOP304 | 過剰な継承深度 | ATTENTION |
| OOP305 | 抽象があるにもかかわらず具象型へ依存 | WARNING |
| OOP306 | 回避可能な具象生成依存 | WARNING |
| OOP307 | 継承よりcompositionが適切な可能性 | ATTENTION |
| OOP401 | 1class内に複数objectが存在する可能性 | WARNING |
| OOP402 | 貧血オブジェクト候補 | WARNING |
| OOP403 | 無関係な依存の過剰保持 | WARNING |
| OOP404 | オブジェクト内部構造の過剰navigation | ATTENTION |
| OOP405 | getter/setterだけのオブジェクト候補 | ATTENTION |

OOP106は1.0.0で唯一、同一rule内でseverityを上げる実装を持ちます。public readonly fieldや不要なpublic setterはWARNING、public mutable fieldや内部のmutable storageをそのまま公開するpropertyはDANGERです。release fixtureの現在のOOP106 positive caseはWARNING経路を固定しており、DANGER経路はrule regression testで別途固定します。

## OOP001 共通抽象の欠如

複数の型が明確に同じ概念上の種類として扱われているにもかかわらず、適切な共通抽象が存在しない場合に警告します。

根拠には、類似したpublicな振る舞い、互換性のあるsignature、繰り返される型分岐、共通のcall siteなどを利用できます。

## OOP002 型分岐による多態性の迂回

共有抽象で振る舞いを表現できる状況で、呼び出し側が具象runtime typeやtype codeによる分岐を繰り返している場合に警告します。

## OOP003 不要な抽象

interfaceやabstract typeが意味のある共通概念を表さず、儀礼的に存在しているだけと判断できる場合に注意します。単一実装の抽象でも意図的な拡張点になり得るため、判定は保守的に行います。

## OOP101 過剰な可視性

型やmemberが、実際の利用に必要な範囲より広く公開されている場合に警告します。カプセル化の破壊が構造的に明確な場合にのみ強く扱います。

例: 親子hierarchy内でしか使われない型がpublicになっている。

## OOP102 sealed候補

外部からの拡張を意図せず、既知の派生型もなく、継承用途が確認できない型をsealed相当の候補として注意します。

## OOP103 static member候補

instance stateへ依存しないmethodをstatic候補として注意します。

## OOP104 static class候補

意味のあるinstance stateを持たず、多態性・DI・identityなどinstanceである理由もないclassをstatic形態の候補として注意します。

## OOP105 stateful static設計

static classが可変な共有状態を蓄積し、global stateとして振る舞っている場合に警告します。

## OOP106 カプセル化漏れ

内部表現を直接外部へ公開している場合に警告します。直接変更可能なpublic stateや、内部の可変storageをそのまま公開する設計は、所有オブジェクトを迂回して変更できるためDangerとします。一方、不必要なpublic setterのように、より軽度の公開はWarningに留める場合があります。

## OOP107 オブジェクト不変条件の迂回

本来オブジェクト自身が保護すべき値を呼び出し側が直接変更でき、不正な状態へ遷移させられる場合に警告します。確度の高い直接的な不変条件迂回はDangerになり得ます。

## OOP108 過剰な外部状態操作

別classが、対象オブジェクトへ振る舞いを依頼する代わりに、その内部状態を日常的に読み書きしている場合に警告します。

## OOP201 巨大な主要操作

オブジェクトの主要操作が過度に大きい、または複雑な場合に警告します。

行数だけで判断せず、complexity、nesting、branch、local state、明確に異なる処理phaseなどを根拠として組み合わせます。

## OOP301 疑わしい継承関係

意味のある親子関係ではなく、主にコード再利用のために継承していると判断できる場合に警告します。

## OOP302 親契約の大部分を未使用

子型が広い親契約を継承しているにもかかわらず、その一部しか利用・対応していない場合に警告します。

## OOP303 子が親の振る舞いを無効化

子の実装が、本来対応すべき親操作を構造的に拒否・無効化している場合はDangerとします。例として、overrideが常に`NotSupportedException`を投げる場合があります。

## OOP304 過剰な継承深度

プロジェクト自身が所有するclass継承chainが深くなり、振る舞いの理解が難しくなる場合にAttentionを出します。既定のしきい値は深度`4`で、`ruleSettings.oop304.warningDepth`から変更できます。最小値は`1`です。

深度は、同一解析で読み込まれた全projectを跨いで数えます。project referenceの境界も含みます。一方、framework、runtime、NuGetその他外部libraryの祖先はproject側の深度として数えず、解析対象project setに属さないbase typeへ到達した時点で打ち切ります。interfaceはclass継承深度へ含めません。

深い継承は、意図的なframework hierarchyやdomain hierarchyで正当化される場合もあるため、不正なOOPの定義ではなく補助的なsignalとして扱います。

## OOP305 抽象があるにもかかわらず具象型へ依存

共有抽象が存在し、その抽象で表現できる振る舞いしか必要としていないにもかかわらず、呼び出し側が具象子型へ直接依存している場合に警告します。

抽象が存在するだけでは違反としません。consumerが具象型固有の振る舞い、property、event、または別の具象固有契約を実際に必要としている場合、その具象依存は意図的な可能性があり、機械的には報告しません。

## OOP306 回避可能な具象生成依存

置換可能な振る舞いを持つcollaborator、または既に適切な抽象を持つcollaboratorをclass内部で直接生成し続けている場合に警告します。

`new`式そのものは違反ではありません。value object、所有する内部object、immutable data、creatorがlifecycleを明確に所有するobject、factoryがreturnする生成、nested object graph構築、明示的なcomposition root/bootstrap配線は正当な直接生成です。このルールは、置換可能なservice-like dependencyについて確度の高いケースに絞ります。

## OOP307 継承よりcompositionが適切な可能性

子型が主に親実装を利用するためだけに継承し、親との意味的関係が弱い場合に、composition候補として保守的に注意します。親の振る舞いの大部分をoverride/hideする場合などが強いsignalです。

## OOP401 1class内に複数objectが存在する可能性

「1class = 1責務」は強制しません。

1つのclass内部に、それぞれ独立して存在できる複数のstate/behavior clusterがあると判断できる場合に警告します。

有用なsignal:
- 異なるmethod群から使われる分離したfield群
- 分離したdependency cluster
- 異なるlifecycle pattern
- 共有stateがほとんど、または全くない
- 一方のclusterを取り除いても、もう一方のidentityが変わらない

1.0.0ではOOP401のseverityはWARNINGで固定します。検出confidenceが不足する場合はseverityを変更するのではなく、報告しない方向で保守的に判定します。

## OOP402 貧血オブジェクト候補

オブジェクト自身は主にstateを保持するだけで、そのstateを所有すべき意味のある振る舞いのほぼすべてが外部serviceに置かれている場合に警告します。

DTO、serialization model、database recordなど、役割が明示されたdata carrierは正当なケースとして扱います。

data carrierの除外判定は共通classifierで行います。明示的なserialization/data-contract属性（例: `Serializable`、`DataContract`、XML/JSON/MessagePack/ProtoBuf系の既知marker）は強い肯定signalとして扱います。型名の`Dto`、`Request`、`Message`、`Model`などは補助signalに留め、名前だけでは除外しません。命名signalを使う場合は、publicなstateを持ち、publicな通常methodによる意味のある振る舞いを持たないdata-shaped typeであることも必要です。通常のdomain objectがpublic stateを公開している場合は、名前がそれらしいだけでOOP106/OOP107/OOP108/OOP402/OOP405等から除外しません。

## OOP403 無関係な依存の過剰保持

オブジェクトが、自身のidentityと明確な関係を持たない多数のsubsystemまたはdependency groupを直接知っている場合に警告します。

## OOP404 オブジェクト内部構造の過剰navigation

`a.B.C.D.DoSomething()`のように深いobject chainを繰り返し辿り、他objectの内部graphへ強く依存している場合に注意します。

Law of Demeterから着想を得ていますが、単純なdot数だけでは判定しません。fluent API、LINQ pipeline、builder、immutable value transformation、namespace、通常のstatic qualificationでは長いchainが正当な場合があります。

object境界を跨ぐnavigationを意味的に検出することを優先します。

## OOP405 getter/setterだけのオブジェクト候補

classの大部分が単純なgetter/setterで構成され、そのstateに対する意味のある操作が外部で実装されている場合に注意します。

これはOOP402を補助するsignalであり、自動的なDangerではありません。明示的なdata carrierはOOP402と同じ共通classifierで除外します。

## metricを使った補助signal

チェッカーは次のような従来metricを利用できます。

- LOC
- method数
- field数
- cyclomatic complexity
- 最大nesting深度
- LCOMまたは同種のcohesion metric
- coupling / dependency数
- 継承深度

これらのmetricは根拠であり、オブジェクト指向として正しいかどうかの定義ではありません。

特に次を守ります。

- classが大きいだけで自動的に不正とはしない。
- method数が多いだけで複数責務とは判断しない。
- low cohesionは、独立したobject clusterも確認できる場合にのみOOP401の根拠を強める。
- high complexityはOOP201の根拠を強めるものであり、一般的なOOP違反へ変換しない。
- SOLID系ルールを将来option/supporting analysisとして追加することはできるが、このprojectではSOLIDをOOPと同一には定義しない。

## severity model

- `DANGER`: このprojectがOOPを名乗る上で基本的とするルールに対する、構造的に明確な違反。既定ではCIを失敗させます。
- `WARNING`: OOP設計として通常は望ましくない、または疑わしいが、致命的とは限らず、contextによって正当化される場合があるもの。
- `ATTENTION`: 厳格な設計では避ける候補だが、強制すると柔軟性低下、bottleneck、framework制約との衝突など意味のあるtrade-offが生じ得るもの。

severityは設計上の影響を表し、検出confidenceだけを表すものではありません。重要な概念であってもheuristic ruleは保守的に判定します。

## suppressionとCI設定

projectは`oop-design-checker.json`で次を設定できます。

- `ignoredPaths`でpathを解析対象から除外する。
- `disabledRules`で特定rule IDをそのproject/runでは出力しない。
- `failureThreshold`でCIを失敗させるseverityを選ぶ。
- `ruleSettings`で、OOP304の継承深度など対応するrule固有heuristicを調整する。

suppressionはproject側の判断であり、rule自体のseverity定義は変えません。rule固有設定はheuristicの感度を変えるもので、severityの意味は変えません。チェッカー自身では原則として自己違反を抑制せず、`attention`しきい値でself-checkします。
