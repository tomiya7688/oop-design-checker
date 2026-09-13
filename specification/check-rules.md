# Check Rules (Draft)

This file records candidate checks discussed during design.

## OOP001 Missing common abstraction

Warn when multiple types are clearly handled as the same conceptual kind but no common abstraction exists.

Signals may include similar public behavior, compatible signatures, repeated type branching, and common call sites.

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

## OOP201 Oversized main operation

Warn when an object's primary operation is excessively large or complex.

Use more than line count: complexity, nesting, branching, local state, and distinct processing phases should contribute to confidence.

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

## Severity idea

- INFO: suggestion or low-confidence improvement
- WARN: probable design issue
- ERROR: structurally clear violation that can be detected with high confidence
