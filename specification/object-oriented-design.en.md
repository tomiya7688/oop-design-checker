# Object-Oriented Design Definition

[日本語（正本）](object-oriented-design.md)

> The Japanese specification is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

## 1.0.0 scope

This document is the **formal design contract for OOP Design Checker 1.0.0**. The analysis backend implemented in 1.0.0 targets C# and uses Roslyn / MSBuild syntax and semantic information. C++, Go, Python, and mixed-language analysis belong to 1.1.0 or later and are not represented here as implemented 1.0.0 capabilities.

The rules defined here detect design-problem candidates from evidence that static analysis can observe. Concepts such as abstraction intent, inheritance intent, object boundaries, data-carrier roles, and dependency ownership cannot be proven mechanically in every codebase, so the implementation is deliberately **heuristic and conservative**. Diagnostics are signals for design review rather than universal judgments of OOP correctness across every domain or framework.

The normative 1.0.0 rule IDs, severities, exemptions, and configuration contract are defined in [Check Rules](check-rules.en.md). Positive and boundary behavior for every implemented rule is fixed by `tests/fixtures/release-validation-csharp/` and its exact release CI.

This repository defines its own checkable interpretation of object-oriented design.

The checker does not judge whether OOP itself is good or bad. It analyzes whether a project that claims to use object-oriented design follows the rules defined here.

## Fundamental principles

The primary checks are derived from the three basic OOP principles:

- Encapsulation
- Inheritance
- Polymorphism

The checker may also use supporting structural rules when they are necessary to make those principles work in actual code.

## Encapsulation

- Internal mutable state should not be exposed more than necessary.
- Visibility should be the minimum required by actual usage.
- Public setters, fields, collections, and methods should exist only when external access is truly required.
- An object should preserve its own valid state and invariants where practical.
- External classes should not need to manipulate another object's internal representation directly.

## Inheritance

- Inheritance should represent a meaningful parent/child relationship, not merely code reuse.
- A child should make meaningful use of the parent contract.
- A child that disables or rejects major parts of the parent behavior is suspicious.
- Inheritance depth and unnecessary extensibility should be detected.
- Types that are not intended to be inherited should be candidates for sealed form where supported.

## Polymorphism

- Types that are clearly handled as the same conceptual kind should have an appropriate common abstraction.
- Repeated type checks and large type-switch branches are candidates for polymorphic replacement.
- Callers should not routinely bypass a shared abstraction by depending directly on concrete child types.
- Interfaces, abstract classes, traits, protocols, or equivalent abstractions should only be introduced when they represent a real shared concept.

## Object integrity and supporting structure

- A class may have multiple behaviors. The checker does not enforce one class = one responsibility.
- The checker should warn when multiple clearly independent objects appear to be packed into one class.
- State and the behavior that conceptually owns that state should not be split apart without a clear reason.
- Data-only objects with all meaningful behavior moved into an external service may be flagged as an anemic-object candidate, while DTOs and similar transport models must remain valid use cases.
- Classes with no meaningful instance state should be candidates for static form where the language supports it, unless instance identity, polymorphism, DI, or extensibility gives the instance meaning.
- Stateful static classes that become global mutable state should be warned about.
- Very large primary operations should be decomposed into meaningful functions when their internal complexity indicates multiple processing phases.
- Dependency relationships should remain explicit and reasonably bounded; an object should not know about unrelated subsystems without need.

## Checker implementation policy

The checker itself is written in C# and must follow the object-oriented design rules defined by this project strictly.

The checker is treated as a primary self-test target. A rule that the checker reports against its own implementation should be regarded as a defect in either the implementation, the rule definition, or the analyzer accuracy and must be reviewed rather than ignored by default.

The implementation should therefore prefer clear object boundaries, appropriate abstractions, minimal visibility, correct use of static/sealed modifiers, meaningful function decomposition, and explicit dependency relationships.

## Non-goals

- Do not define SOLID as identical to OOP.
- Do not reject classes only because they are large.
- Do not force interfaces mechanically.
- Do not enforce one class = one responsibility.
- Do not treat every data-only type as invalid; DTO/value/serialization models are legitimate when their role is explicit.
