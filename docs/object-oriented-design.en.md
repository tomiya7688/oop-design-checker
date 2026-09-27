# Object-Oriented Design Definition

[日本語（正本）](object-oriented-design.md)

> The Japanese document is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

## Status of this document

This document describes **the definition of object-oriented design adopted by this project**.

OOP has multiple historical and practical interpretations. This document does not claim to be the one universally correct definition. It states a project-specific position so design decisions can be discussed consistently.

The concepts are not derived from the constraints of any particular programming language, framework, or tool.

## What is an object?

An object is not merely "a value instantiated from a class." It is a **boundary that owns state, behavior, and invariants for a coherent conceptual responsibility**.

Identity may matter for some objects while value equality matters for others. What matters is that ownership, external promises, and hidden internal details are clear.

## Ownership of state and behavior

- Keep state near the behavior that understands its meaning and constraints where practical.
- Do not scatter state-transition rules across unrelated callers; let the owner provide the operations that change its state.
- Even stateless behavior should have a clear conceptual owner.
- Mechanically separating "data" from "processing" does not by itself make a design object-oriented.

## Encapsulation

Encapsulation is more than applying a private modifier.

- Separate internal representation from the contract consumers need.
- Do not expose mutable state more widely than necessary.
- Reduce paths that let callers bypass invariants and mutate internals directly.
- Public APIs should express what can be done while minimizing reliance on how data is stored.
- Keep visibility no wider than required for real collaboration.

## Object invariants and integrity

An object should preserve its valid state where practical.

- Do not expose unrestricted operations that can freely create invalid states.
- When several pieces of state must remain consistent, place operations that preserve that consistency at the owning boundary.
- Reconsider ownership when an external service must manipulate an object's state in a particular sequence for the object to remain valid.
- When external orchestration is necessary, make the reason and responsibility boundary explicit.

## Object boundaries

A useful object boundary separates internal detail from external collaboration.

- Avoid designs that require callers to navigate deeply through another object's internals.
- Ask collaborators for capabilities instead of obtaining their internal data structures and performing their work externally.
- Moving data across a boundary is not inherently wrong. The concern is persistent external dependence on internal representation.

## Polymorphism

Polymorphism means **multiple implementations can provide behavior through the same meaningful conceptual contract**.

- Consider a shared contract for things that are genuinely handled as the same kind.
- Prefer delegating behavior to the participating object when callers would otherwise enumerate concrete types repeatedly.
- Do not invent abstractions solely to claim polymorphism; the shared contract must have meaning.

## Abstraction

Abstraction hides implementation detail and expresses the conceptual contract consumers need.

- Do not measure abstraction quality by the presence of interfaces or abstract classes alone.
- Avoid ceremonial abstractions that do not represent a real shared concept.
- Abstract when hiding implementation improves changeability, substitutability, or comprehension.
- Do not ban an abstraction merely because it currently has one implementation; judge whether the boundary itself is meaningful.

## Inheritance and composition

Inheritance is a strong relationship that carries "is-a" meaning and inherited contract.

- Do not choose inheritance merely for code reuse.
- A child used as its parent should meaningfully satisfy the parent's contract.
- Reconsider the relationship when children routinely reject or disable major parent operations.
- Prefer composition when the goal is to combine or replace independent roles.
- Neither inheritance nor composition is universally correct; choose based on meaning and expected direction of change.

## Dependencies

A dependency is a collaboration relationship between objects.

- Knowing required collaborators is not inherently a problem.
- Avoid broad dependency on unrelated subsystems that imports many unrelated reasons to change.
- Consider directing dependencies toward conceptually stable boundaries.
- Separate creation responsibility from use responsibility when useful.
- Dependency injection or any particular wiring mechanism is not itself the definition of OOP.

## Object integrity and multiple responsibilities

This project does not define OOP as "one class = one responsibility."

An object may have many behaviors when they remain coherent around the same state, invariants, and identity.

Conversely, when multiple concepts do not share state or behavior and change independently but are merely packed into one class, their object boundaries may deserve separation.

## Intentional data carriers, values, and DTOs

Not every type needs rich behavior.

Data-centered types can be legitimate, including:

- DTOs, messages, and serialization models
- immutable values
- query results and projections
- data contracts crossing boundaries
- data shapes required by a framework

The important distinction is whether being a data carrier is the intended role, or whether behavior and invariants that belong to an object were accidentally pushed outside it.

## What this project does not equate with OOP

None of the following, by itself, defines OOP here:

- SOLID compliance
- class or method line counts
- number of interfaces
- number of design patterns
- use of a dependency-injection container
- making everything a class
- "one class = one responsibility"
- presence of getters/setters
- use of inheritance itself

These can affect design quality in context, but they do not replace the meaning of object boundaries, ownership, contracts, and collaboration.

## Trade-offs and non-goals

- Performance, interoperability, framework contracts, serialization, memory layout, and similar constraints may make an ideal object boundary impractical.
- The goal is not zero exceptions; it is clear reasons and clear responsibility for exceptions.
- Other paradigms such as functional programming and data-oriented design are not rejected.
- Do not force this definition onto parts of a system that intentionally do not use OOP.
- Fully automating design judgment is not a goal.
