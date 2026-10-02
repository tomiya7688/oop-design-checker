"""Pure helpers for reporting native Appium E2E failures."""

import json
from pathlib import Path


def build_wait_context(active_context, condition, automation_id, timeout, source):
    return {
        **active_context,
        "action": f"wait until: {condition}",
        "expectedCondition": condition,
        "automationId": automation_id,
        "timeoutSeconds": timeout,
        "source": source,
    }


def build_failure_report(
    exception,
    traceback_text,
    *,
    context=None,
    evidence_paths=None,
    evidence_errors=None,
):
    context = context or {}
    message = str(exception)
    return {
        "exceptionType": type(exception).__name__,
        "message": message,
        "messageIsEmpty": not bool(message.strip()),
        "scenario": context.get("scenario"),
        "action": context.get("action"),
        "expectedCondition": context.get("expectedCondition"),
        "automationId": context.get("automationId"),
        "timeoutSeconds": context.get("timeoutSeconds"),
        "source": context.get("source"),
        "failureLocation": context.get("failureLocation"),
        "lastSuccessfulAction": context.get("lastSuccessfulAction"),
        "traceback": traceback_text,
        "evidence": evidence_paths or {},
        "evidenceErrors": evidence_errors or [],
    }


def format_failure_report(report):
    message = report["message"]
    lines = [
        "Appium UI E2E failed.",
        f"Exception: {report['exceptionType']}: {message or '(empty message)'}",
        f"Scenario: {report.get('scenario') or '(unknown)'}",
        f"Action: {report.get('action') or '(unknown)'}",
        f"Expected condition: {report.get('expectedCondition') or '(not recorded)'}",
        f"Target element: {report.get('automationId') or '(not recorded)'}",
        f"Timeout: {report.get('timeoutSeconds') or '(not applicable)'} seconds",
    ]
    if report.get("source"):
        lines.append(f"Source: {report['source']}")
    if report.get("lastSuccessfulAction"):
        lines.append(f"Last successful action: {report['lastSuccessfulAction']}")
    lines.append("Evidence:")
    lines.extend(
        f"  {name}: {path}" for name, path in report.get("evidence", {}).items()
    )
    if report.get("evidenceErrors"):
        lines.append("Evidence capture errors:")
        lines.extend(f"  {error}" for error in report["evidenceErrors"])
    lines.extend(("Traceback:", report.get("traceback") or "(not available)"))
    return "\n".join(lines)


def write_failure_report(path, report):
    Path(path).write_text(
        json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8"
    )


def capture_evidence(name, callback):
    try:
        callback()
    except Exception as error:
        return f"{name}: {type(error).__name__}: {error}"
    return None
