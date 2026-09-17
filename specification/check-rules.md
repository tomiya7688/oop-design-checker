# チェックルール仕様

> **この日本語版が正本です。** 英語互換版は [en/check-rules.md](en/check-rules.md) を参照してください。内容に差異がある場合は、この日本語版を優先します。

この文書は、本プロジェクトが定義する検査ルールを記録します。

## OOP001 共通抽象の欠如
複数の型が明確に同じ概念種として扱われているのに、適切な共通抽象が存在しない場合に警告します。似たpublic behavior、互換signature、type branchingの反復、共通call siteなどを根拠として使えますが、単なる名前の一致だけでは判定しません。

## OOP002 型分岐によるポリモーフィズム迂回
共有抽象で自然に表現できる振る舞いを、callerが具象runtime typeやtype codeで繰り返し分岐している場合に警告します。単発のvalidation guard等は機械的に違反扱いしません。

## OOP003 不要な抽象
interfaceやabstract typeが有意味な共有概念を持たず、儀礼的にしか存在していないと判断できる場合に注意します。単一実装でも意図的な拡張点になり得るため、保守的に判定します。marker interfaceを自動的に不要抽象とは扱いません。

## OOP101 過剰な可視性
型/memberが実際の利用範囲より広く公開されている場合に警告します。カプセル化破壊が構造的に明確な場合だけ強く扱います。framework/library契約によって必要な公開範囲は考慮します。

## OOP102 sealed候補
外部拡張の意図がなく、既知の派生型もなく、継承される意味が確認できない型を `sealed` 候補として注意します。framework契約等の拡張点は除外します。

## OOP103 static member候補
instance stateへ依存しないmethodをstatic候補として注意します。ただしpolymorphism、framework callback、DI、instance identity等の理由がある場合は除外します。

## OOP104 static class候補
有意味なinstance stateを持たず、polymorphism、DI、identity、framework管理等の理由もないclassをstatic class候補として注意します。

## OOP105 stateful static設計
static classやstatic memberへ可変共有状態が蓄積し、global mutable stateとして振る舞う場合に警告します。参照自体がreadonlyでも中身が可変なcollectionは共有可変状態になり得ます。

## OOP106 カプセル化漏れ
内部表現を直接外部へ公開している場合に警告します。public mutable fieldや内部mutable storageの直接公開など、callerが所有objectを迂回して直接変更できる高確度ケースは `DANGER` とします。不要なpublic setterなど、比較的軽い公開は `WARNING` に留めます。明示的data carrierは役割に応じて除外します。

## OOP107 オブジェクト不変条件の迂回
object自身が守るべき値をcallerが直接変更し、不正状態へできる場合に警告します。明確に不変条件を迂回できる場合は `DANGER` とします。setter自身が妥当性検証を行う場合は機械的に違反扱いしません。

## OOP108 過剰な外部状態操作
別classがobjectへ振る舞いを依頼せず、そのobjectの内部状態を繰り返し読み書きして操作している場合に警告します。明示的data carrierや外部library型は対象外です。

## OOP201 巨大な主処理
objectの主要operationが過度に大きい、または複雑な場合に警告します。単純な行数だけでなく、statement数、cyclomatic complexity、nesting、local state、state member利用、複数の処理phaseなどを組み合わせて判定します。local function等で親operationを機械的に水増ししません。

## OOP301 疑わしい継承関係
継承が意味のある親子関係ではなく、主として実装再利用のために使われていると判断できる場合に警告します。project-ownedな親子関係を中心に見て、単なる同名memberだけでは判定しません。

## OOP302 親契約の大部分を未使用
子型が広い親契約を継承したものの、実際には要求・提供されている操作の大部分をno-op/default化している場合に警告します。親側でも任意no-op hookとして定義されている操作は、必須契約として数えません。

## OOP303 子が親の振る舞いを無効化
子実装が、本来サポートすべき親operationを構造的に拒否・無効化する場合に `DANGER` とします。例: 親の有効なoperationやabstract requirementをoverrideし、常に `NotSupportedException` を投げる。親自身が元からunsupportedとして定義しているoperationは新たな違反としません。

## OOP304 過剰な継承深度
project-ownedなclass inheritance chainが深くなり、振る舞いの追跡が難しくなる場合に `ATTENTION` を出します。既定しきい値はdepth `4` で、`ruleSettings.oop304.warningDepth` により最小 `1` から変更できます。

同じ解析へ読み込まれたproject間の継承も数えます。一方、framework、runtime、NuGet等の外部library ancestryはプロジェクトへ課さず、解析対象project集合の外にあるbase typeへ到達した時点で数えるのを止めます。interfaceはclass inheritance depthに含めません。

深い継承は無効OOPの定義ではなく補助シグナルです。domain hierarchyやframework上の正当な設計は存在し得ます。

## OOP305 抽象があるのに具象型へ依存
共有抽象が存在し、その抽象だけでconsumerが使う振る舞いを表現できるのに、callerが具象子型へ直接依存している場合に警告します。抽象が存在するだけでは不十分で、consumerが具象固有のbehavior/property/event等を本当に必要としている場合は許容します。

## OOP306 回避可能な具象生成依存
置換可能なcollaboratorに適切な抽象があるにもかかわらず、利用側objectがその具象実装を直接生成している高確度ケースで警告します。`new` 自体を違反とは定義しません。value object、所有内部object、factory-owned return、nested object graph、composition root/bootstrapでのwiring等は正当な直接生成です。

## OOP307 継承よりcompositionが適切な可能性
子型が親との意味的なis-a関係より、親のprotected実装を材料として得るために継承していると判断できる場合に、保守的に注意します。正当なoverride specializationは除外します。

## OOP401 1クラス内に複数objectが存在する可能性
`1 class = 1 responsibility` は強制しません。

1つのclass内に、独立して存在できる複数のstate/behavior clusterが含まれているように見える場合に警告します。

有効な根拠例:
- 互いに交わらないfield groupとmethod group
- 独立したdependency cluster
- 異なるlifecycle
- 共有stateがほとんどない
- 一方のclusterを除去しても他方のidentityが変わらない

単純なmember数やclass sizeだけでは判定しません。

## OOP402 貧血オブジェクト候補
objectがほぼstate保持だけを行い、そのstateを所有すべき有意味なbehaviorの大半が外部serviceに置かれている場合に警告します。DTO、serialization model、database record等の明示的data carrierは正当な用途として除外します。

## OOP403 無関係な依存の過多
objectが、自身のidentityと明確な関係のない複数のsubsystem/dependency groupを直接知っている場合に警告します。単純なdependency数だけでは判定しません。

## OOP404 オブジェクト内部への過剰なナビゲーション
project objectの境界を深く繰り返し辿り、別objectの内部graphへcallerが強く依存している場合に注意します。Law of Demeterに着想を得ますが、raw dot countだけでは判定しません。fluent API、LINQ pipeline、builder、immutable value transformation、namespace、static qualification等の長いchainは正当になり得ます。

## OOP405 getter/setterだけのobject候補
classの大部分がtrivial getter/setterで、有意味なstate operationが外部に置かれている場合に注意します。OOP402の補助シグナルであり、自動的な `DANGER` にはしません。明示的data carrierは除外します。

## メトリクス補助シグナル

以下のような一般的メトリクスを証拠として利用できます。

- LOC
- method count
- field count
- cyclomatic complexity
- maximum nesting depth
- LCOM等のcohesion metric
- coupling / dependency count
- inheritance depth

これらはオブジェクト指向として正しい/誤りの定義ではありません。

特に:
- large classだけで無効としない。
- methodが多いだけでmultiple responsibilityとは判定しない。
- low cohesionは、独立object clusterも確認できる場合にOOP401の根拠を強める。
- high complexityはOOP201の根拠を強めるが、汎用的なOOP違反にはしない。
- SOLIDを追加する場合もoptional/supporting analysisとし、OOPそのものと同一視しない。

## Severityモデル

- `DANGER` / 危険: 「OOPとして扱う以上、構造的に許容できない」と本プロジェクトが定義する高重大度違反。既定ではCIを失敗させる。
- `WARNING` / 警告: OOP設計として通常は好ましくない、または疑わしいが、文脈によって正当化され得る。
- `ATTENTION` / 注意: 厳密な設計上の助言。強制すると柔軟性低下、bottleneck、framework制約等の実質的trade-offが生じ得る。

severityは設計上の影響を表し、検出confidenceそのものではありません。heuristic ruleは重要な概念を扱う場合でも保守的に判定します。

## 抑制とCI設定

projectは `oop-design-checker.json` で以下を設定できます。

- `ignoredPaths`: path除外
- `disabledRules`: rule ID単位の診断抑制
- `failureThreshold`: CI失敗level
- `ruleSettings`: OOP304 inheritance depth等の対応済みheuristic調整

抑制はproject側の判断であり、rule severityの定義を変更しません。rule-specific tuningもseverityではなく感度を変更します。チェッカー自身は原則として自分のrule violationを抑制せず、`attention` thresholdで自己検査します。
