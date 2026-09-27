# Check Rules — 1.0.0 Normative Specification

[日本語（正本）](check-rules.md)

> The Japanese specification is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

> Target version: **1.0.0**. This document is the normative definition of the C# rules implemented by 1.0.0.

This document defines the checks, severities, known exclusion boundaries, and configuration contract that are implemented and release-gated in 1.0.0. Conceptual future candidates are not treated as implemented rules.

## 1.0.0 rule catalog

The 1.0.0 rule IDs and severity contract are listed below. Severity is the fixed or default severity for the rule.

| Rule | Meaning | Severity |
| --- | --- | --- |
| OOP001 | Missing common abstraction | WARNING |
| OOP002 | Polymorphism bypassed by type branching | WARNING |
| OOP003 | Unnecessary abstraction | ATTENTION |
| OOP101 | Excessive visibility | WARNING |
| OOP102 | Sealing candidate | ATTENTION |
| OOP103 | Static member candidate | ATTENTION |
| OOP104 | Static class candidate | ATTENTION |
| OOP105 | Stateful static design | WARNING |
| OOP106 | Encapsulation leak | WARNING (DANGER for high-confidence directly mutable exposure) |
| OOP107 | Object invariant can be bypassed | DANGER |
| OOP108 | Excessive external state manipulation | WARNING |
| OOP201 | Oversized main operation | WARNING |
| OOP301 | Suspicious inheritance relationship | WARNING |
| OOP302 | Parent contract mostly unused | WARNING |
| OOP303 | Child disables parent behavior | DANGER |
| OOP304 | Excessive inheritance depth | ATTENTION |
| OOP305 | Concrete-type dependency despite abstraction | WARNING |
| OOP306 | Avoidable concrete construction dependency | WARNING |
| OOP307 | Composition may be more appropriate than inheritance | ATTENTION |
| OOP401 | Possible multiple objects in one class | WARNING |
| OOP402 | Anemic object candidate | WARNING |
| OOP403 | Excessive unrelated dependencies | WARNING |
| OOP404 | Excessive navigation through object internals | ATTENTION |
| OOP405 | Getter/setter-only object candidate | ATTENTION |

OOP106 is the only 1.0.0 rule that raises severity within the same rule. A public readonly field or unnecessarily public setter is WARNING; a public mutable field or property that directly exposes mutable internal storage is DANGER. The current release-fixture OOP106 positive case locks the WARNING path, while the DANGER path is covered separately by rule regression tests.

## OOP001 Missing common abstraction

Warn when multiple types are clearly handled as the same conceptual kind but no common abstraction exists.

Signals may include similar public behavior, compatible signatures, repeated type branching, and common call sites.

## OOP002 Polymorphism bypassed by type branching

Warn when callers repeatedly branch on concrete runtime types or type codes where behavior could reasonably be expressed through a shared abstraction.

## OOP003 Unnecessary abstraction

Attention when an interface or abstract type has no meaningful shared concept and appears to exist only as ceremony. This must be conservative because single-implementation abstractions can still be intentional extension points.

## OOP101 Excessive visibility

Warn when a type/member is more visible than its actual usage requires. Escalate only when the encapsulation break is structurally clear.

Example: a type used only inside a parent/child hierarchy is declared public.

## OOP102 Sealing candidate

Attention when a type is not externally extensible, has no known derived types, and is not intended for inheritance.

## OOP103 Static member candidate

Attention for methods that do not depend on instance state.

## OOP104 Static class candidate

Attention for classes that have no meaningful instance state and no polymorphic/DI/identity reason to be instantiated.

## OOP105 Stateful static design

Warn when a static class accumulates mutable shared state and behaves as global state.

## OOP106 Encapsulation leak

Report WARNING or DANGER when internal representation is exposed directly. A public mutable field or a property that directly exposes mutable internal storage is DANGER because callers can bypass the owning object. A public readonly field or unnecessarily public setter, where direct mutability is more limited, is WARNING.

## OOP107 Object invariant can be bypassed

Report DANGER when the analyzer can structurally establish that callers can bypass validation and place an object into an invalid state by directly mutating a value that the object otherwise guards. OOP107 is fixed at DANGER in 1.0.0.

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

Attention conservatively when a child type uses inheritance mainly to obtain implementation while its semantic relationship to the parent is weak, especially when it overrides or hides a large part of the inherited behavior.

## OOP401 Possible multiple objects in one class

Do not enforce one class = one responsibility.

Warn when a class appears to contain multiple independent state/behavior clusters that could exist separately.

Useful signals:
- field groups used by disjoint method groups
- separate dependency clusters
- separate lifecycle patterns
- little or no shared state
- one cluster can be removed without changing the identity of another

In 1.0.0, OOP401 is fixed at WARNING. When detection confidence is insufficient, the analyzer should conservatively avoid reporting rather than change this severity.

## OOP402 Anemic object candidate

Warn when an object mainly stores state while nearly all meaningful behavior that owns that state lives in an external service.

DTOs, serialization models, database records, and other explicit data-carrier types must remain valid cases.

Data-carrier exemptions use one shared classifier. Explicit serialization/data-contract attributes (for example `Serializable`, `DataContract`, and known XML/JSON/MessagePack/ProtoBuf markers) are strong positive signals. Naming such as `Dto`, `Request`, `Message`, or `Model` is only a supporting signal and is not sufficient by itself. When naming contributes to the exemption, the type must also be data-shaped: it exposes public state and has no ordinary public behavior methods. An ordinary domain object with public state is still eligible for OOP106/OOP107/OOP108/OOP402/OOP405 even when its name resembles a data carrier.

## OOP403 Excessive unrelated dependencies

Warn when an object directly knows about many unrelated subsystems or dependency groups with no clear relation to its identity.

## OOP404 Excessive navigation through object internals

Attention when code repeatedly traverses deep object chains such as `a.B.C.D.DoSomething()` and therefore depends on the internal object graph of another object.

This is inspired by the Law of Demeter, but raw dot-counting must not be used as the only criterion. Fluent APIs, LINQ-style pipelines, builders, immutable value transformations, namespaces, and ordinary static qualification can legitimately contain long chains.

Prefer semantic detection of repeated navigation across object boundaries.

## OOP405 Getter/setter-only object candidate

Attention when a class is overwhelmingly composed of trivial getters and setters while meaningful operations on its state are implemented elsewhere.

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

## 1.0.0 C# analysis boundary

All 24 rules above are implemented by the C# backend in 1.0.0.

- For `.csproj`, `.sln`, `.slnx`, and project directories, analysis uses the real Roslyn/MSBuild project compilation as the unit of analysis. Unrelated projects are not merged into one artificial compilation.
- When project references are loaded into the same analysis, rules may inspect symbol relationships across that project set where required.
- A loose `.cs` file is analyzed as a standalone compilation without requiring a project file. Rules that depend on MSBuild project metadata therefore have less context than project/solution analysis.
- OOP101 and OOP102 run only for **application projects**, where assumptions about external visibility and extensibility can be made safely. The same assumptions are not mechanically applied to library or standalone contexts.
- Framework/runtime/NuGet symbols outside the analyzed project set are not treated as project-owned types.
- C++, Go, Python, and mixed-language analysis are outside the 1.0.0 implementation scope.

## Severity model

- `DANGER`: a structurally clear violation of a rule this project considers fundamental to claiming OOP design. These fail CI by default.
- `WARNING`: normally undesirable or suspicious OOP design, but not inherently fatal and sometimes justified by context.
- `ATTENTION`: strict-design guidance where enforcing the rule can reduce flexibility, introduce a bottleneck, conflict with framework constraints, or otherwise carry meaningful tradeoffs.

Severity expresses design impact, not detection confidence alone. A heuristic rule should remain conservative even if the underlying concept is important.

## Suppression and CI configuration

The configuration file is optional. Without an explicit path, the checker searches from the target directory upward for the nearest `oop-design-checker.json`; if none is found, built-in defaults are used.

The 1.0.0 built-in defaults are:

- `ignoredPaths = []`
- `disabledRules = []`
- `failureThreshold = danger`
- `ruleSettings.oop304.warningDepth = 4`

Supported settings:

- `ignoredPaths` excludes matching paths from analysis.
- `disabledRules` suppresses selected rule IDs for that project/run. Rule ID matching is case-insensitive.
- `failureThreshold` selects the minimum severity that makes the process fail: `attention`, `warning`, or `danger`. Numeric enum values are rejected.
- `ruleSettings.oop304.warningDepth` changes the OOP304 inheritance-depth threshold. The minimum value is `1`.

When `--config <path>` is explicitly supplied, that configuration must exist; the checker does not silently fall back to defaults. A relative path is checked against the current working directory first and then against the target directory.

CLI `--fail-on <severity>` overrides the configuration file's `failureThreshold` for that run. `--warnings-as-errors` remains a compatibility alias for `--fail-on warning`.

Suppression is a project decision and does not change the rule's defined severity. Rule-specific tuning changes heuristic sensitivity, not the severity definition. The checker project itself should not suppress its own rule violations and should self-check at the `attention` threshold.
