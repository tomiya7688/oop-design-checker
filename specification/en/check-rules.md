# Check Rules

> **English compatibility edition.** The Japanese [check-rules.md](../check-rules.md) is the canonical specification. If this translation differs from the Japanese version, the Japanese version takes precedence.

This file records the checks defined by the project.

## OOP001 Missing common abstraction
Warn when multiple types are clearly handled as the same conceptual kind but no common abstraction exists. Signals may include similar public behavior, compatible signatures, repeated type branching, and common call sites.

## OOP002 Polymorphism bypassed by type branching
Warn when callers repeatedly branch on concrete runtime types or type codes where behavior could reasonably be expressed through a shared abstraction.

## OOP003 Unnecessary abstraction
Warn when an interface or abstract type has no meaningful shared concept and appears to exist only as ceremony. This must be conservative because single-implementation abstractions can still be intentional extension points.

## OOP101 Excessive visibility
Warn when a type/member is more visible than its actual usage requires. Escalate only when the encapsulation break is structurally clear.

## OOP102 Sealing candidate
Suggest sealing when a type is not externally extensible, has no known derived types, and is not intended for inheritance.

## OOP103 Static member candidate
Suggest static for methods that do not depend on instance state.

## OOP104 Static class candidate
Suggest static form for classes that have no meaningful instance state and no polymorphic/DI/identity reason to be instantiated.

## OOP105 Stateful static design
Warn when a static class accumulates mutable shared state and behaves as global state.

## OOP106 Encapsulation leak
Warn when internal representation is exposed directly. Directly mutable public state and directly exposed mutable storage are Danger-level violations. Less severe exposure may remain Warning.

## OOP107 Object invariant can be bypassed
Warn when callers can place an object into an invalid state by directly mutating values that should be guarded by the object itself. High-confidence direct invariant bypasses may be Danger.

## OOP108 Excessive external state manipulation
Warn when another class routinely reads and writes the internal state of an object instead of asking that object to perform the behavior itself.

## OOP201 Oversized main operation
Warn when an object's primary operation is excessively large or complex. Use more than line count: complexity, nesting, branching, local state, and distinct processing phases should contribute to confidence.

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
Warn when a shared abstraction exists but callers routinely downcast or depend on concrete child types to perform behavior already covered by that abstraction. The existence of an abstraction alone is not enough; concrete-only behavior can justify a concrete dependency.

## OOP306 Avoidable concrete construction dependency
Warn when a class constructs replaceable concrete collaborators directly even though a suitable abstraction exists. A `new` expression by itself is not a violation. Value objects, owned internal objects, factory-owned return values, nested object-graph construction, and explicit composition-root/bootstrap wiring are valid direct constructions.

## OOP307 Composition may be more appropriate than inheritance
Warn conservatively when a child type uses inheritance mainly to obtain implementation while its semantic relationship to the parent is weak.

## OOP401 Possible multiple objects in one class
Do not enforce one class = one responsibility. Warn when a class appears to contain multiple independent state/behavior clusters that could exist separately.

Useful signals include disjoint field/method groups, separate dependency clusters, separate lifecycle patterns, little shared state, and removable clusters that do not change another cluster's identity.

## OOP402 Anemic object candidate
Warn when an object mainly stores state while nearly all meaningful behavior that owns that state lives in an external service. Explicit DTOs, serialization models, database records, and other data carriers remain valid.

## OOP403 Excessive unrelated dependencies
Warn when an object directly knows about many unrelated subsystems or dependency groups with no clear relation to its identity.

## OOP404 Excessive navigation through object internals
Warn when code repeatedly traverses deep project object chains and therefore depends on another object's internal graph. This is inspired by the Law of Demeter, but raw dot counting is not sufficient; fluent APIs, LINQ pipelines, builders, immutable transformations, namespaces, and static qualification can legitimately contain long chains.

## OOP405 Getter/setter-only object candidate
Warn when a class is overwhelmingly composed of trivial getters and setters while meaningful operations on its state are implemented elsewhere. This supports OOP402 and does not apply to explicit data carriers.

## Metric-assisted signals
The checker may use LOC, method/field counts, cyclomatic complexity, nesting depth, cohesion metrics, coupling/dependency counts, and inheritance depth as evidence. Metrics are not definitions of object-oriented correctness.

## Severity model
- `DANGER`: a structurally clear violation of a rule this project considers fundamental to claiming OOP design. These fail CI by default.
- `WARNING`: normally undesirable or suspicious OOP design, but not inherently fatal and sometimes justified by context.
- `ATTENTION`: strict-design guidance where enforcing the rule can reduce flexibility, introduce a bottleneck, conflict with framework constraints, or otherwise carry meaningful tradeoffs.

Severity expresses design impact, not detection confidence alone. Heuristic rules must remain conservative.

## Suppression and CI configuration
Projects may use `oop-design-checker.json` to exclude paths with `ignoredPaths`, suppress selected rule IDs with `disabledRules`, choose the CI failure level with `failureThreshold`, and tune supported heuristics under `ruleSettings`.

Suppression is a project decision and does not change a rule's severity. The checker project itself should not suppress its own violations and should self-check at the `attention` threshold.
