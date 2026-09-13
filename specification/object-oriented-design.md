# Object-Oriented Design Definition (Draft)

This repository defines its own checkable interpretation of object-oriented design.

The checker does not judge whether OOP itself is good or bad. It analyzes whether a project that claims to use object-oriented design follows the rules defined here.

## Core ideas

- Commonly handled types should have an appropriate common abstraction.
- Visibility and modifiers should match actual usage.
- Very large main operations should be decomposed into meaningful functions.
- Classes with no meaningful instance state should be candidates for static form where the language supports it.
- A class may have multiple behaviors. The checker does not enforce one class = one responsibility.
- The checker should warn when multiple clearly independent objects appear to be packed into one class.

## Non-goals

- Do not define SOLID as identical to OOP.
- Do not reject classes only because they are large.
- Do not force interfaces mechanically.
- Do not enforce one class = one responsibility.
