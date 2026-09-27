# オブジェクト指向設計の定義

[English](object-oriented-design.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

## この文書の位置づけ

この文書は、**このprojectが採用するオブジェクト指向設計の定義**を記述します。

オブジェクト指向には歴史的・実務的に複数の説明があります。この文書は唯一絶対の定義を主張せず、設計判断の基準を明確にするためのproject固有の立場を示します。

特定のprogramming language、framework、toolの都合から概念を逆算しません。

## Objectとは何か

objectは単に「classから生成された値」ではなく、**ある概念上の責任範囲について状態・振る舞い・不変条件をひとまとまりとして所有する境界**です。

object identityが重要な場合も、valueとしての同一性が重要な場合もあります。重要なのは、何をそのobjectが所有し、何を外部へ約束し、何を内部詳細として隠すかが明確であることです。

## 状態と振る舞いの所有

- 状態は、その状態の意味と制約を理解する振る舞いと可能な範囲で同じ境界に置く。
- 状態変更の規則を外部へ散在させず、所有者が変更方法を提供する。
- 状態を持たない振る舞いでも、概念上どのobjectの責務かを明確にする。
- 「data」と「処理」を機械的に別層へ分けること自体をobject-orientedとはみなさない。

## Encapsulation

encapsulationは単なるprivate modifierの利用ではありません。

- object内部の表現を、利用者が知る必要のある契約から分離する。
- 可変状態を必要以上に公開しない。
- 外部から不変条件を迂回して状態を書き換えられる経路を減らす。
- 公開APIは「何ができるか」を表し、「内部でどう保持しているか」への依存を最小化する。
- 可視性は実際の協調に必要な範囲へ抑える。

## Object invariant / integrity

objectは、実用上可能な範囲で自身の正しい状態を守ります。

- 有効でない状態を作れる操作を無制限に公開しない。
- 複数の状態要素の整合性が必要なら、その整合性を守る操作をobject境界に置く。
- 外部serviceがobject内部の状態を順番に書き換えなければ成立しない設計は、所有境界を再検討する。
- 例外的に外部調整が必要な場合は、その理由と責任境界を明確にする。

## Object boundary

良いobject boundaryでは、内部詳細と外部協調の境界が明確です。

- 他objectの深い内部構造をたどり続けなければ仕事ができない設計を避ける。
- 協調相手には必要な能力を依頼し、内部data構造を取得して代わりに処理しない。
- 境界をまたぐdata transfer自体は問題ではない。問題は、内部表現への継続的な依存が外部へ漏れることです。

## Polymorphism

polymorphismは、**同じ概念上の契約に対して複数の実装が振る舞いを提供できること**です。

- 同じ種類として扱う対象には、共有できる意味のある契約を検討する。
- 呼び出し側が具体型を列挙し続けるより、対象自身の振る舞いへ委譲できるならその方を優先する。
- polymorphismを導入するためだけの抽象化は避ける。共有契約に意味があることが前提です。

## Abstraction

abstractionは実装詳細を隠し、利用者が必要とする概念上の契約を表現します。

- interfaceやabstract typeの有無だけで抽象化の質を判断しない。
- 実在しない共通概念を作るための儀礼的な抽象を増やさない。
- 具体実装を隠すことで変更可能性、置換可能性、理解容易性が改善する場合に抽象化する。
- 1実装しかないことだけを理由に抽象を禁止もしない。境界として意味があるかで判断する。

## Inheritance と composition

inheritanceは「is-a」の意味と契約継承を伴う強い関係です。

- 単なるcode reuseだけを目的にinheritanceを選ばない。
- childはparentとして扱われたとき、parentの契約を意味のある形で満たす。
- childがparentの主要操作を恒常的に拒否・無効化するなら、関係を再検討する。
- 独立した役割の組み合わせや差し替えが目的ならcompositionを優先して検討する。
- inheritanceとcompositionのどちらかを常に正解とはしない。関係の意味と変更方向で選ぶ。

## Dependency

dependencyはobject間の協調関係です。

- objectが仕事に必要な相手を知ること自体は問題ではない。
- 無関係なsubsystemへ広く依存し、変更理由が大量に流入する状態を避ける。
- dependencyの方向は、概念上の安定した境界へ向けることを検討する。
- 生成責務と利用責務は、必要に応じて分離する。
- dependency injection等の特定手法をOOPそのものとは定義しない。

## Object integrity と複数責務

「1 class = 1責務」をこのprojectのOOP定義にはしません。

1つのobjectが複数の振る舞いを持っていても、それらが同じ状態・不変条件・identityを中心にまとまっているなら一貫したobjectになり得ます。

逆に、互いに状態も振る舞いも協調せず、独立して変更される複数の概念が偶然1つのclassへ詰め込まれているなら、object boundaryを分ける余地があります。

## Data carrier / value / DTO の意図的例外

すべての型が豊富な振る舞いを持つ必要はありません。

次のような役割はdata中心でも正当です。

- DTO / message / serialization model
- immutable value
- query result / projection
- boundaryを越えるためのdata contract
- frameworkが要求するdata shape

重要なのは、**data carrierであることが意図された役割か**、本来objectが守るべき不変条件や振る舞いを外部へ追い出した結果なのかを区別することです。

## このprojectでOOPと同一視しないもの

次のものを、単独ではOOPの定義としません。

- SOLIDへの準拠
- classやmethodの行数
- interfaceの数
- design patternの使用数
- dependency injection containerの利用
- すべてをclassにすること
- 「1 class = 1責務」
- getter/setterの存在そのもの
- inheritanceの使用そのもの

これらは文脈によって設計品質へ影響しますが、object boundary・状態所有・契約・協調関係の意味を置き換えるものではありません。

## Trade-off と非目標

- performance、interop、framework contract、serialization、memory layout等の制約により、理想的なobject boundaryをそのまま採用できない場合があります。
- 例外をゼロにすることより、例外の理由と責任範囲を明確にすることを重視します。
- functional programmingやdata-oriented design等、他のparadigmを否定しません。
- OOPを採用しない部分へ無理にこの定義を適用しません。
- 設計判断を完全に機械化することを目標にしません。
