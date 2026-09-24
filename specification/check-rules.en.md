# Check Rules

[日本語（正本）](check-rules.md)

> The Japanese specification is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

This document is the **formal C# rule contract for OOP Design Checker 1.0.0**. Only the OOPxxx rules listed here are implemented rules for 1.0.0.

## 1.0.0 rule contract

The analysis language for 1.0.0 is C#. The following 24 rule IDs and severity contracts are normative. OOP106 is the only rule in this table that explicitly permits per-finding escalation beyond its normal severity.

| Rule | Severity contract | Note |
| --- | --- | --- |
| OOP001 | `WARNING` |  |
| OOP002 | `WARNING` |  |
| OOP003 | `ATTENTION` |  |
| OOP101 | `WARNING` |  |
| OOP102 | `ATTENTION` |  |
| OOP103 | `ATTENTION` |  |
| OOP104 | `ATTENTION` |  |
| OOP105 | `WARNING` |  |
| OOP106 | `WARNING → DANGER` | Normally WARNING; high-confidence direct exposure of mutable storage may escalate to DANGER. |
| OOP107 | `DANGER` |  |
| OOP108 | `WARNING` |  |
| OOP201 | `WARNING` |  |
| OOP301 | `WARNING` |  |
| OOP302 | `WARNING` |  |
| OOP303 | `DANGER` |  |
| OOP304 | `ATTENTION` |  |
| OOP305 | `WARNING` |  |
| OOP306 | `WARNING` |  |
| OOP307 | `ATTENTION` |  |
| OOP401 | `WARNING` |  |
| OOP402 | `WARNING` |  |
| OOP403 | `WARNING` |  |
| OOP404 | `ATTENTION` |  |
| OOP405 | `ATTENTION` |  |

`DANGER`, `WARNING`, and `ATTENTION` follow the severity model below. `failureThreshold` changes only the process failure decision; it does not rewrite the severity of emitted diagnostics.

### Heuristics and exemption principles

- Rules use observable syntax / semantic usage and should not establish a serious finding from one weak signal such as a type name or LOC alone.
- Explicit DTOs, serialization models, database records, and similar data carriers are not mechanically classified into OOP106/OOP107/OOP108/OOP402/OOP405 merely because they hold state.
- Framework/runtime/library contracts, external ancestry, and framework/value-type navigation are not treated as equivalent to project-owned design violations.
- Intentional direct construction such as value objects, owned internal objects, and composition roots is not mechanically classified as OOP306.
- Overlapping findings may be coordinated/deduplicated so the same design problem is not reported as multiple equally strong findings. This does not change rule meaning or severity contracts.


## OOP001 Missing common abstraction

Warn when multiple types are clearly handled as the same conceptual kind but no common abstraction exists.

Signals may include similar public behavior, compatible signatures, repeated type branching, and common call sites.

## OOP002 Polymorphism bypassed by type branching

Warn when callers repeatedly branch on concrete runtime types or type codes where behavior could reasonably be expressed through a shared abstraction.

## OOP003 Unnecessary abstraction

Raise Attention when an interface or abstract type has no meaningful shared concept and appears to exist only as ceremony. This must be conservative because single-implementation abstractions can still be intentional extension points.

## OOP101 Excessive visibility

Warn when a type/member is more visible than its actual usage requires. Escalate only when the encapsulation break is structurally clear.

Example: a type used only inside a parent/child hierarchy is declared public.

## OOP102 Sealing candidate

Suggest sealing when a type is not externally extensible, has no known derived types, and is not intended for inheritance.

## OOP103 Static member candidate

Suggest static for methods that do not depend on instance state.

## OOP104 Static class candidate

Suggest static form for classes that have no meaningful instance state and no polymorphic/DI/identity reason to be instantiated.

## OOP105 Stateful static design

Warn when a static class accumulates mutable shared state and behaves as global state.

## OOP106 Encapsulation leak

Warn when internal representation is exposed directly. Directly mutable public state and directly exposed mutable storage are Danger-level violations because callers can bypass the owning object. Less severe exposure, such as an unnecessarily public setter whose use is still controlled, may remain Warning.

## OOP107 Object invariant can be bypassed

Treat a direct, structurally clear bypass of an object's invariant as Danger when callers can place the object into an invalid state by mutating values that should be guarded by the object itself.

## OOP108 Excessive external state manipulation

Warn when another class routinely reads and writes the internal state of an object instead of asking that object to perform the behavior itself.

## OOP201 Oversized main operation

Warn when an object's primary operation is excessively large or complex.

Use more than line count: complexity, nesting, branching, local state, and distinct processing phases should contribute to confidence.

## OOP301 Suspicious inheritance relationship

Warn when inheritance appears to be used mainly for code reuse rather than a meaningful parent/child relationship.

## OOP302 Parent contract mostly unused

Warn when a child type inherits a broad parent contract but uses or supports only a small part of it.

## OOP303 Child disables parent behavior

Danger when a child implementation structurally rejects or disables a parent operation it is expected to support, for example an override that always throws `NotSupportedException`.

## OOP304 Excessive inheritance depth

Attention when the project-owned class inheritance chain becomes deep enough to make behavior difficult to reason about. The default attention threshold is depth `4`, configurable through `ruleSettings.oop304.warningDepth` with a minimum value of `1`.

Depth is counted across every project loaded in the same analysis, including project-reference boundaries. Framework, runtime, NuGet, and other external-library ancestry is not charged to the project; counting stops at the first base type whose assembly is outside the analyzed project set. Interfaces are not part of this class-inheritance depth.

Deep inheritance remains a supporting signal rather than a definition of invalid OOP because deliberate framework or domain hierarchies can still justify it.

## OOP305 Concrete-type dependency despite abstraction

Warn when a shared abstraction exists but callers routinely downcast or depend on concrete child types to perform behavior already covered by that abstraction.

The existence of an abstraction alone is not enough. If the consumer actually requires concrete-only behavior, properties, events, or another concrete-only contract, the concrete dependency can be intentional and should not be reported mechanically.

## OOP306 Avoidable concrete construction dependency

Warn when a class repeatedly constructs concrete collaborators directly even though those collaborators represent replaceable behavior or already have a suitable abstraction.

A `new` expression by itself is not a violation. Value objects, owned internal objects, immutable data, objects whose lifecycle clearly belongs to the creator, factory-owned return values, nested object-graph construction, and explicit composition-root/bootstrap wiring are valid direct constructions. This rule should focus on replaceable service-like dependencies and high-confidence cases.

## OOP307 Composition may be more appropriate than inheritance

Raise Attention conservatively when a child type uses inheritance mainly to obtain implementation while its semantic relationship to the parent is weak, especially when it overrides or hides a large part of the inherited behavior.

## OOP401 Possible multiple objects in one class

Do not enforce one class = one responsibility.

Warn when a class appears to contain multiple independent state/behavior clusters that could exist separately.

Useful signals:
- field groups used by disjoint method groups
- separate dependency clusters
- separate lifecycle patterns
- little or no shared state
- one cluster can be removed without changing the identity of another

High-confidence cases may become Danger; uncertain cases remain Warning or Attention.

## OOP402 Anemic object candidate

Warn when an object mainly stores state while nearly all meaningful behavior that owns that state lives in an external service.

DTOs, serialization models, database records, and other explicit data-carrier types must remain valid cases.

Data-carrier exemptions use one shared classifier. Explicit serialization/data-contract attributes (for example `Serializable`, `DataContract`, and known XML/JSON/MessagePack/ProtoBuf markers) are strong positive signals. Naming such as `Dto`, `Request`, `Message`, or `Model` is only a supporting signal and is not sufficient by itself. When naming contributes to the exemption, the type must also be data-shaped: it exposes public state and has no ordinary public behavior methods. An ordinary domain object with public state is still eligible for OOP106/OOP107/OOP108/OOP402/OOP405 even when its name resembles a data carrier.

## OOP403 Excessive unrelated dependencies

Warn when an object directly knows about many unrelated subsystems or dependency groups with no clear relation to its identity.

## OOP404 Excessive navigation through object internals

Raise Attention when code repeatedly traverses deep object chains such as `a.B.C.D.DoSomething()` and therefore depends on the internal object graph of another object.

This is inspired by the Law of Demeter, but raw dot-counting must not be used as the only criterion. Fluent APIs, LINQ-style pipelines, builders, immutable value transformations, namespaces, and ordinary static qualification can legitimately contain long chains.

Prefer semantic detection of repeated navigation across object boundaries.

## OOP405 Getter/setter-only object candidate

Raise Attention when a class is overwhelmingly composed of trivial getters and setters while meaningful operations on its state are implemented elsewhere.

This is a supporting signal for OOP402 rather than an automatic Danger. Explicit data-carrier types are exempt through the same shared classifier used by OOP402.

## Metric-assisted signals

The checker may calculate traditional metrics such as:

- LOC
- method count
- field count
- cyclomatic complexity
- maximum nesting depth
- LCOM or similar cohesion metrics
- coupling / dependency counts
- inheritance depth

These metrics are evidence, not definitions of object-oriented correctness.

In particular:

- a large class is not automatically invalid;
- many methods do not automatically mean multiple responsibilities;
- low cohesion should strengthen OOP401 only when independent object clusters can also be identified;
- high complexity should strengthen OOP201 rather than becoming a generic OOP violation;
- SOLID rules may be provided as optional or supporting analyses, but SOLID is not defined as identical to OOP in this project.

## Severity model

- `DANGER`: a structurally clear violation of a rule this project considers fundamental to claiming OOP design. These fail CI by default.
- `WARNING`: normally undesirable or suspicious OOP design, but not inherently fatal and sometimes justified by context.
- `ATTENTION`: strict-design guidance where enforcing the rule can reduce flexibility, introduce a bottleneck, conflict with framework constraints, or otherwise carry meaningful tradeoffs.

Severity expresses design impact, not detection confidence alone. A heuristic rule should remain conservative even if the underlying concept is important.

## Suppression and CI configuration

When no configuration file is specified, the checker searches from the target directory upward for the nearest `oop-design-checker.json`. An explicitly requested `--config` that is missing or invalid is a runtime error and must not silently fall back to built-in defaults.

Projects may use `oop-design-checker.json` to:

- exclude paths with `ignoredPaths`;
- suppress selected rule IDs for a project/run with `disabledRules`;
- choose the CI failure level with `failureThreshold`;
- tune supported rule-specific heuristics under `ruleSettings`. In 1.0.0 this includes `ruleSettings.oop304.warningDepth`, default `4`, minimum `1`.

Suppression is a project decision and does not change the rule's defined severity. Rule-specific tuning changes heuristic sensitivity, not the severity definition. The checker project itself should not suppress its own rule violations and should self-check at the `attention` threshold.
