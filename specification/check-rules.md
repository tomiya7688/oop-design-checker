# Check Rules (Draft)

This file records candidate checks discussed during design.

## OOP001 Missing common abstraction

Warn when multiple types are clearly handled as the same conceptual kind but no common abstraction exists.

Signals may include similar public behavior, compatible signatures, repeated type branching, and common call sites.

## OOP002 Polymorphism bypassed by type branching

Warn when callers repeatedly branch on concrete runtime types or type codes where behavior could reasonably be expressed through a shared abstraction.

## OOP003 Unnecessary abstraction

Warn when an interface or abstract type has no meaningful shared concept and appears to exist only as ceremony. This must be conservative because single-implementation abstractions can still be intentional extension points.

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

Warn when callers can place an object into an invalid state by directly mutating values that should be guarded by the object itself. High-confidence direct invariant bypasses may be Danger.

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

Attention when the inheritance chain becomes deep enough to make behavior difficult to reason about. Thresholds should be configurable and should not be the only signal because deep inheritance can be intentional.

## OOP305 Concrete-type dependency despite abstraction

Warn when a shared abstraction exists but callers routinely downcast or depend on concrete child types to perform behavior already covered by that abstraction.

The existence of an abstraction alone is not enough. If the consumer actually requires concrete-only behavior, properties, events, or another concrete-only contract, the concrete dependency can be intentional and should not be reported mechanically.

## OOP306 Avoidable concrete construction dependency

Warn when a class repeatedly constructs concrete collaborators directly even though those collaborators represent replaceable behavior or already have a suitable abstraction.

A `new` expression by itself is not a violation. Value objects, owned internal objects, immutable data, objects whose lifecycle clearly belongs to the creator, factory-owned return values, nested object-graph construction, and explicit composition-root/bootstrap wiring are valid direct constructions. This rule should focus on replaceable service-like dependencies and high-confidence cases.

## OOP307 Composition may be more appropriate than inheritance

Warn conservatively when a child type uses inheritance mainly to obtain implementation while its semantic relationship to the parent is weak, especially when it overrides or hides a large part of the inherited behavior.

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

## OOP403 Excessive unrelated dependencies

Warn when an object directly knows about many unrelated subsystems or dependency groups with no clear relation to its identity.

## OOP404 Excessive navigation through object internals

Warn when code repeatedly traverses deep object chains such as `a.B.C.D.DoSomething()` and therefore depends on the internal object graph of another object.

This is inspired by the Law of Demeter, but raw dot-counting must not be used as the only criterion. Fluent APIs, LINQ-style pipelines, builders, immutable value transformations, namespaces, and ordinary static qualification can legitimately contain long chains.

Prefer semantic detection of repeated navigation across object boundaries.

## OOP405 Getter/setter-only object candidate

Warn when a class is overwhelmingly composed of trivial getters and setters while meaningful operations on its state are implemented elsewhere.

This is a supporting signal for OOP402 rather than an automatic Danger. Explicit data-carrier types are exempt.

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

Projects may use `oop-design-checker.json` to:

- exclude paths with `ignoredPaths`;
- suppress selected rule IDs for a project/run with `disabledRules`;
- choose the CI failure level with `failureThreshold`.

Suppression is a project decision and does not change the rule's defined severity. The checker project itself should not suppress its own rule violations and should self-check at the `attention` threshold.
