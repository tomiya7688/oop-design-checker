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

Warn or error when a type/member is more visible than its actual usage requires.

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

Warn when mutable internal representation is exposed directly, for example through public mutable fields, unnecessary public setters, or mutable collections returned without protection.

## OOP107 Object invariant can be bypassed

Warn when callers can place an object into an invalid state by directly mutating values that should be guarded by the object itself.

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

Warn or error when a child implementation routinely rejects, disables, or throws for major parent operations.

## OOP304 Excessive inheritance depth

Warn when the inheritance chain becomes deep enough to make behavior difficult to reason about. Thresholds should be configurable and should not be the only signal.

## OOP305 Concrete-type dependency despite abstraction

Warn when a shared abstraction exists but callers routinely downcast or depend on concrete child types to perform normal behavior.

## OOP401 Possible multiple objects in one class

Do not enforce one class = one responsibility.

Warn when a class appears to contain multiple independent state/behavior clusters that could exist separately.

Useful signals:
- field groups used by disjoint method groups
- separate dependency clusters
- separate lifecycle patterns
- little or no shared state
- one cluster can be removed without changing the identity of another

High-confidence cases may become errors; uncertain cases remain warnings.

## OOP402 Anemic object candidate

Warn when an object mainly stores state while nearly all meaningful behavior that owns that state lives in an external service.

DTOs, serialization models, database records, and other explicit data-carrier types must remain valid cases.

## OOP403 Excessive unrelated dependencies

Warn when an object directly knows about many unrelated subsystems or dependency groups with no clear relation to its identity.

## Severity idea

- INFO: suggestion or low-confidence improvement
- WARN: probable design issue
- ERROR: structurally clear violation that can be detected with high confidence
