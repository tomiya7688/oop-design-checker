#!/usr/bin/env python3
import argparse
import json
import re
from pathlib import Path

EXPECTED = {
    "OOP001": "WARNING",
    "OOP002": "WARNING",
    "OOP003": "ATTENTION",
    "OOP101": "WARNING",
    "OOP102": "ATTENTION",
    "OOP103": "ATTENTION",
    "OOP104": "ATTENTION",
    "OOP105": "WARNING",
    "OOP106": "WARNING → DANGER",
    "OOP107": "DANGER",
    "OOP108": "WARNING",
    "OOP201": "WARNING",
    "OOP301": "WARNING",
    "OOP302": "WARNING",
    "OOP303": "DANGER",
    "OOP304": "ATTENTION",
    "OOP305": "WARNING",
    "OOP306": "WARNING",
    "OOP307": "ATTENTION",
    "OOP401": "WARNING",
    "OOP402": "WARNING",
    "OOP403": "WARNING",
    "OOP404": "ATTENTION",
    "OOP405": "ATTENTION",
}

DEFAULT_SEVERITY = {
    rule_id: ("WARNING" if rule_id == "OOP106" else contract)
    for rule_id, contract in EXPECTED.items()
}

ALLOWED_FIXTURE_SEVERITIES = {
    rule_id: ({"warning", "danger"} if rule_id == "OOP106" else {contract.lower()})
    for rule_id, contract in EXPECTED.items()
}


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    return parser.parse_args()


def first_heading(text):
    for line in text.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return ""


def rule_headings(text):
    return set(re.findall(r"^## (OOP\d{3})\b", text, flags=re.MULTILINE))


def severity_table(text):
    result = {}
    pattern = re.compile(r"^\| (OOP\d{3}) \| `([A-Z]+(?: → [A-Z]+)?)` \|", re.MULTILINE)
    for rule_id, severity in pattern.findall(text):
        if rule_id in result:
            raise AssertionError(f"Duplicate severity table row for {rule_id}.")
        result[rule_id] = severity
    return result


def implementation_descriptors(root):
    result = {}
    pattern = re.compile(
        r'new\(\s*"(OOP\d{3})"\s*,\s*"[^"]+"\s*,\s*'
        r"DesignDiagnosticSeverity\.(Attention|Warning|Danger)\s*\)",
        re.MULTILINE,
    )
    for path in sorted((root / "src" / "OopDesignChecker" / "Rules").glob("*.cs")):
        for rule_id, severity in pattern.findall(path.read_text(encoding="utf-8-sig")):
            if rule_id in result:
                raise AssertionError(
                    f"Duplicate implementation descriptor for {rule_id}: "
                    f"{result[rule_id][1]} and {path}."
                )
            result[rule_id] = (severity.upper(), path.as_posix())
    return result


def fixture_contract(root):
    path = root / "tests" / "fixtures" / "release-validation-csharp" / "expected-diagnostics.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    required = data["requiredDiagnostics"]
    exact = data["exactDiagnostics"]
    return required, exact


def validate(root):
    errors = []
    jp_design = (root / "specification" / "object-oriented-design.md").read_text(encoding="utf-8-sig")
    en_design = (root / "specification" / "object-oriented-design.en.md").read_text(encoding="utf-8-sig")
    jp_rules = (root / "specification" / "check-rules.md").read_text(encoding="utf-8-sig")
    en_rules = (root / "specification" / "check-rules.en.md").read_text(encoding="utf-8-sig")

    headings = {
        "object-oriented-design.md": first_heading(jp_design),
        "object-oriented-design.en.md": first_heading(en_design),
        "check-rules.md": first_heading(jp_rules),
        "check-rules.en.md": first_heading(en_rules),
    }
    for name, heading in headings.items():
        if not heading:
            errors.append(f"{name}: missing H1 heading.")
        if "ドラフト" in heading or "draft" in heading.casefold():
            errors.append(f"{name}: draft marker remains in H1: {heading!r}.")

    expected_ids = set(EXPECTED)
    for name, text in (("check-rules.md", jp_rules), ("check-rules.en.md", en_rules)):
        ids = rule_headings(text)
        if ids != expected_ids:
            errors.append(
                f"{name}: OOP rule headings differ. "
                f"missing={sorted(expected_ids - ids)} extra={sorted(ids - expected_ids)}"
            )
        table = severity_table(text)
        if table != EXPECTED:
            errors.append(
                f"{name}: severity table differs. expected={EXPECTED} actual={table}"
            )

    implementation = implementation_descriptors(root)
    implementation_map = {rule_id: severity for rule_id, (severity, _) in implementation.items()}
    if set(implementation_map) != expected_ids:
        errors.append(
            "Implementation descriptors differ. "
            f"missing={sorted(expected_ids - set(implementation_map))} "
            f"extra={sorted(set(implementation_map) - expected_ids)}"
        )
    for rule_id, expected_default in DEFAULT_SEVERITY.items():
        actual = implementation_map.get(rule_id)
        if actual is not None and actual != expected_default:
            errors.append(
                f"{rule_id}: implementation default severity {actual} != {expected_default}."
            )

    required, exact = fixture_contract(root)
    required_ids = {item["ruleId"] for item in required}
    exact_ids = {item["ruleId"] for item in exact}
    if required_ids != expected_ids:
        errors.append(
            "Release fixture requiredDiagnostics rule IDs differ. "
            f"missing={sorted(expected_ids - required_ids)} "
            f"extra={sorted(required_ids - expected_ids)}"
        )
    if exact_ids != expected_ids:
        errors.append(
            "Release fixture exactDiagnostics does not exercise every rule. "
            f"missing={sorted(expected_ids - exact_ids)} "
            f"extra={sorted(exact_ids - expected_ids)}"
        )

    for item in required:
        rule_id = item["ruleId"]
        severity = item["severity"].lower()
        allowed = ALLOWED_FIXTURE_SEVERITIES.get(rule_id)
        if allowed is not None and severity not in allowed:
            errors.append(
                f"{rule_id}: required fixture severity {severity} not in {sorted(allowed)}."
            )
    for item in exact:
        rule_id = item["ruleId"]
        severity = item["severity"].lower()
        allowed = ALLOWED_FIXTURE_SEVERITIES.get(rule_id)
        if allowed is not None and severity not in allowed:
            errors.append(
                f"{rule_id}: exact fixture severity {severity} not in {sorted(allowed)}."
            )

    return errors


def main():
    args = parse_args()
    root = Path(args.root).resolve()
    errors = validate(root)
    if errors:
        print("Specification contract validation failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print(
        "Specification contract passed: "
        f"{len(EXPECTED)} rule IDs, JP/EN severity tables, implementation descriptors, "
        "and release fixture coverage agree."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
