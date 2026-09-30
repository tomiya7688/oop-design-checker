# Object-Oriented Design

[English](README.en.md)

> この日本語版を正本とします。英語版との間に差異がある場合は、日本語版を優先します。

このrepositoryのmain productは、**「このprojectで言うオブジェクト指向とは何か」**を定義する文書です。

この定義は唯一絶対のオブジェクト指向を主張するものではありません。設計判断を共有・議論・検証するために、このprojectが採用する立場を明文化したものです。

特に、object boundary、encapsulation、状態と振る舞いの所有、inheritance / composition、polymorphism、abstractionを重視する、現在のclass-based application designに近い実務的な解釈です。message passingやobject間協調をより中心に置く歴史的なOOP観とは重点が異なることを前提としています。

## 正本文書

- [オブジェクト指向設計の定義](docs/object-oriented-design.md)
- [Object-Oriented Design Definition (English)](docs/object-oriented-design.en.md)

日本語版を正本とし、英語版は追従版です。

本文は特定のlanguage、framework、compiler、静的解析toolに依存しません。checkerで機械的に検知できない原則も、設計上重要であれば本文に含めます。

## 補助tool

[OOP Design Checker](tools/oop-design-checker/README.md) は、本文の原則のうち静的に観測できるsignalを補助確認するtoolです。

```text
文書 = 正本 / main product
checker = 補助tool
```

checkerのrule、severity、C# / Roslyn / MSBuild、CLI、GUI、config、release package、false-positive回避境界はtool固有の仕様です。これらを本文の定義そのものとは扱いません。

checkerを使わず、文書だけを設計指針として利用できます。

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

GitHub ActionsはGitHubの仕様上rootに置き、tool配下を参照します。

## Versioning

checkerの `1.0.0` は**tool version**です。本文の概念定義をchecker versionと同一視しません。

checker releaseは、その時点の本文に対して「どの原則をどのruleでどこまで補助検査できるか」というcoverage snapshotを持ちます。本文が更新されても、未実装部分をchecker対応済みとはみなしません。
