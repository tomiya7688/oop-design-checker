# Object-Oriented Design

[日本語（正本）](README.md)

> The Japanese documentation is normative. If this English translation differs from the Japanese version, the Japanese version takes precedence.

The main product of this repository is the document that defines **what this project means by object-oriented design**.

This is not a claim that there is one universally correct definition of OOP. It records the design position adopted by this project so design decisions can be shared, discussed, and reviewed consistently.

In particular, it is a practical interpretation close to modern class-based application design, emphasizing object boundaries, encapsulation, ownership of state and behavior, inheritance and composition, polymorphism, and abstraction. It intentionally acknowledges that historical interpretations centered more strongly on message passing and object collaboration place emphasis elsewhere.

## Normative document

- [オブジェクト指向設計の定義](docs/object-oriented-design.md)
- [Object-Oriented Design Definition](docs/object-oriented-design.en.md)

The Japanese document is normative and the English document follows it.

The definition is intentionally independent of any specific language, framework, compiler, or static-analysis tool. A principle may belong in the document even when no checker can verify it mechanically.

## Auxiliary tool

[OOP Design Checker](tools/oop-design-checker/README.en.md) is an auxiliary tool that checks statically observable signals for a subset of the principles.

```text
document = normative main product
checker = auxiliary tool
```

Rule IDs, severities, C# / Roslyn / MSBuild behavior, CLI/GUI/configuration, release packaging, and false-positive boundaries are tool-specific concerns. They are not the definition of OOP itself.

The document can be used without using the checker.

## Repository structure

```text
/
├ README.md
├ README.en.md
├ docs/
│  ├ object-oriented-design.md
│  └ object-oriented-design.en.md
├ tools/
│  └ oop-design-checker/
│     ├ README.md
│     ├ README.en.md
│     ├ OopDesignChecker.sln
│     ├ src/
│     ├ tests/
│     └ docs/
│        ├ check-rules.md
│        └ check-rules.en.md
└ .github/workflows/
```

GitHub Actions remain at the repository root because GitHub requires that location and refer into the tool directory.

## Versioning

Checker `1.0.0` is a **tool version**. It is not the version of the conceptual OOP definition.

Each checker release is a coverage snapshot describing which parts of the current document are mapped to implemented rules. Updating the document does not automatically mean the checker implements the new or changed principles.

## Python tooling updates

See the [Japanese canonical Python update and CA audit guide](tools/oop-design-checker/docs/python-trust.md) for development and CI tooling. Static CA membership checks do not prove rejection of every compromised or cross-signed chain.

## Release authenticity

Publication remains blocked until trusted Windows/macOS signing is implemented. See the [Japanese canonical provenance and release gate guide](tools/oop-design-checker/docs/release-authenticity.md) for artifact inventory, verification, and outstanding work.

## Local checks before a PR

Run `python check_local.py` from the repository root. See the [canonical Japanese guide](tools/local-check/README.md) for prerequisites, automatic selection and CI boundaries. Missing dependencies or zero targets fail; no automatic restore, installation or fixes run.
