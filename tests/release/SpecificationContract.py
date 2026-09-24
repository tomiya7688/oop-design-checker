#!/usr/bin/env python3
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RULES_DIR = ROOT / "src" / "OopDesignChecker" / "Rules"
JA_RULES = ROOT / "specification" / "check-rules.md"
EN_RULES = ROOT / "specification" / "check-rules.en.md"
JA_DESIGN = ROOT / "specification" / "object-oriented-design.md"
EN_DESIGN = ROOT / "specification" / "object-oriented-design.en.md"
MANIFEST = ROOT / "tests" / "fixtures" / "release-validation-csharp" / "expected-diagnostics.json"

SEVERITY_NAME = {
    "Attention": "ATTENTION",
    "Warning": "WARNING",
    "Danger": "DANGER",
}


def fail(message):
    raise SystemExit(message)


def source_rule_severities():
    pattern = re.compile(
        r'new\(\s*"(?P<id>OOP\d{3})"'
        r'[\s\S]{0,240}?DesignDiagnosticSeverity\.(?P<severity>Attention|Warning|Danger)'
    )
    result = {}
    for path in RULES_DIR.glob("*.cs"):
        text = path.read_text(encoding="utf-8")
        match = pattern.search(text)
        if match is None:
            continue
        rule_id = match.group("id")
        severity = SEVERITY_NAME[match.group("severity")]
        if rule_id in result:
            fail(f"Duplicate rule descriptor for {rule_id}.")
        result[rule_id] = severity
    return result


def specification_rule_severities(path):
    text = path.read_text(encoding="utf-8")
    table_pattern = re.compile(
        r"^\|\s*(OOP\d{3})\s*\|[^\n]*?\|\s*(DANGER|WARNING|ATTENTION)",
        re.MULTILINE,
    )
    result = {}
    for rule_id, severity in table_pattern.findall(text):
        if rule_id in result:
            fail(f"{path}: duplicate severity table row for {rule_id}.")
        result[rule_id] = severity

    sections = re.findall(r"^##\s+(OOP\d{3})\b", text, re.MULTILINE)
    if len(sections) != len(set(sections)):
        fail(f"{path}: duplicate OOP rule sections exist.")
    if set(sections) != set(result):
        fail(
            f"{path}: rule section/table mismatch. "
            f"sections={sorted(sections)} table={sorted(result)}"
        )
    return result


def validate_titles_and_normative_markers():
    documents = {
        JA_DESIGN: ("ドラフト", "1.0.0", "この日本語版を正本"),
        JA_RULES: ("ドラフト", "1.0.0", "この日本語版を正本"),
        EN_DESIGN: ("Draft", "1.0.0", "Japanese specification is normative"),
        EN_RULES: ("Draft", "1.0.0", "Japanese specification is normative"),
    }
    for path, (draft_marker, version_marker, normative_marker) in documents.items():
        text = path.read_text(encoding="utf-8")
        first_line = text.splitlines()[0]
        if draft_marker in first_line:
            fail(f"{path}: draft marker remains in the specification title.")
        if version_marker not in first_line:
            fail(f"{path}: title does not identify the 1.0.0 snapshot.")
        if normative_marker not in text:
            fail(f"{path}: normative-language marker is missing.")


def validate_fixture(spec):
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    required = manifest["requiredDiagnostics"]
    exact = manifest["exactDiagnostics"]

    required_map = {}
    for item in required:
        rule_id = item["ruleId"]
        severity = item["severity"].upper()
        if rule_id in required_map:
            fail(f"{MANIFEST}: duplicate requiredDiagnostics entry for {rule_id}.")
        required_map[rule_id] = severity

        fixture_file = MANIFEST.parent / item["file"]
        if not fixture_file.is_file():
            fail(f"{MANIFEST}: required diagnostic file does not exist: {item['file']}")

    exact_by_rule = {}
    for item in exact:
        exact_by_rule.setdefault(item["ruleId"], set()).add(item["severity"].upper())

    if set(required_map) != set(spec):
        fail(
            "release fixture requiredDiagnostics does not cover the specification exactly: "
            f"required={sorted(required_map)} spec={sorted(spec)}"
        )
    if set(exact_by_rule) != set(spec):
        fail(
            "release fixture exactDiagnostics does not cover the specification exactly: "
            f"exact={sorted(exact_by_rule)} spec={sorted(spec)}"
        )

    for rule_id, severity in spec.items():
        if required_map[rule_id] != severity:
            fail(
                f"{MANIFEST}: {rule_id} required severity is "
                f"{required_map[rule_id]}, expected {severity}."
            )
        if severity not in exact_by_rule[rule_id]:
            fail(
                f"{MANIFEST}: {rule_id} exact diagnostics do not include "
                f"the primary severity {severity}: {sorted(exact_by_rule[rule_id])}"
            )


def validate_oop106_dynamic_severity():
    source = (RULES_DIR / "EncapsulationLeakRule.cs").read_text(encoding="utf-8")
    if "DesignDiagnosticSeverity.Danger" not in source:
        fail("OOP106 implementation no longer contains its documented DANGER escalation.")

    for path in (JA_RULES, EN_RULES):
        text = path.read_text(encoding="utf-8")
        oop106_section = text.split("## OOP106", 1)[1].split("## OOP107", 1)[0]
        if "DANGER" not in oop106_section and "Danger" not in oop106_section:
            fail(f"{path}: OOP106 DANGER escalation is not documented.")


def main():
    validate_titles_and_normative_markers()

    source = source_rule_severities()
    japanese = specification_rule_severities(JA_RULES)
    english = specification_rule_severities(EN_RULES)

    if len(source) != 24:
        fail(f"Expected 24 implemented rules, found {len(source)}: {sorted(source)}")
    if japanese != source:
        fail(f"Japanese rule severity table differs from implementation: {japanese} != {source}")
    if english != source:
        fail(f"English rule severity table differs from implementation: {english} != {source}")

    validate_fixture(source)
    validate_oop106_dynamic_severity()

    print("1.0.0 specification contract passed: 24/24 rules and release fixture aligned.")


if __name__ == "__main__":
    main()
