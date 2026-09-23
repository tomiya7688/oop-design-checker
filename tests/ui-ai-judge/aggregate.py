#!/usr/bin/env python3
import argparse
import json
import sys
from pathlib import Path

PROVIDERS = ("openai", "google", "anthropic")
CHECK_IDS = tuple(f"UI-{index:03d}" for index in range(1, 13))


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--waiver")
    return parser.parse_args()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def normalize_provider(provider, directory):
    path = Path(directory) / f"{provider}.json"
    if not path.exists():
        return {"provider": provider, "status": "error", "model": None, "error": "missing result", "result": None}
    data = read_json(path)
    if data.get("provider") != provider:
        return {"provider": provider, "status": "error", "model": data.get("model"), "error": "provider mismatch", "result": None}
    if data.get("status") != "ok" or not isinstance(data.get("result"), dict):
        return {
            "provider": provider,
            "status": "error",
            "model": data.get("model"),
            "error": data.get("error") or "provider error",
            "result": None,
        }
    return data


def aggregate_votes(votes, error_count):
    fails = sum(vote == "fail" for vote in votes)
    reviews = sum(vote == "review" for vote in votes)
    passes = sum(vote == "pass" for vote in votes)
    if error_count >= 2 or fails >= 2:
        return "fail"
    if error_count == 1 or fails == 1 or reviews >= 1:
        return "review_required"
    if passes == 3:
        return "pass"
    return "review_required"


def load_waiver(path, scenario):
    if not path:
        return None
    waiver = read_json(path)
    if waiver.get("scenario") != scenario:
        raise ValueError("Waiver scenario does not match aggregate scenario.")
    if not str(waiver.get("reason", "")).strip():
        raise ValueError("Waiver requires a non-empty reason.")
    if not str(waiver.get("approvedBy", "")).strip():
        raise ValueError("Waiver requires approvedBy.")
    return waiver


def main():
    args = parse_args()
    providers = [normalize_provider(provider, args.results) for provider in PROVIDERS]
    ok_results = [item["result"] for item in providers if item["status"] == "ok"]
    scenarios = {item.get("scenario") for item in ok_results}
    scenarios.discard(None)
    if len(scenarios) != 1:
        raise ValueError(f"Expected exactly one scenario across provider results, found {sorted(scenarios)}.")
    scenario = next(iter(scenarios))
    error_count = sum(item["status"] != "ok" for item in providers)

    checks = []
    for check_id in CHECK_IDS:
        provider_votes = {}
        votes = []
        for provider in providers:
            if provider["status"] != "ok":
                provider_votes[provider["provider"]] = "error"
                continue
            matches = [check for check in provider["result"].get("checks", []) if check.get("id") == check_id]
            if len(matches) != 1:
                provider_votes[provider["provider"]] = "error"
                continue
            verdict = matches[0].get("verdict")
            if verdict not in {"pass", "fail", "review"}:
                provider_votes[provider["provider"]] = "error"
                continue
            provider_votes[provider["provider"]] = verdict
            votes.append(verdict)

        local_errors = sum(value == "error" for value in provider_votes.values())
        checks.append({
            "id": check_id,
            "verdict": aggregate_votes(votes, local_errors),
            "providerVerdicts": provider_votes,
        })

    provider_overall_votes = [
        item["result"].get("verdict")
        for item in providers
        if item["status"] == "ok" and item["result"].get("verdict") in {"pass", "fail", "review"}
    ]
    overall_vote = aggregate_votes(provider_overall_votes, error_count)
    if any(check["verdict"] == "fail" for check in checks):
        verdict = "fail"
    elif overall_vote == "fail":
        verdict = "fail"
    elif (
        overall_vote == "pass"
        and all(check["verdict"] == "pass" for check in checks)
    ):
        verdict = "pass"
    else:
        verdict = "review_required"

    waiver = load_waiver(args.waiver, scenario)
    final_verdict = verdict
    if waiver is not None:
        if verdict != "review_required":
            raise ValueError("Waivers may only resolve REVIEW_REQUIRED.")
        final_verdict = "pass_with_waiver"

    output = {
        "version": 1,
        "scenario": scenario,
        "verdict": verdict,
        "finalVerdict": final_verdict,
        "providers": [
            {
                "provider": item["provider"],
                "model": item.get("model"),
                "status": item["status"],
                "verdict": item["result"].get("verdict") if item["status"] == "ok" else None,
                "error": item.get("error"),
            }
            for item in providers
        ],
        "checks": checks,
        "waiver": waiver,
    }
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(output, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(output, indent=2, ensure_ascii=False))
    return 0 if final_verdict in {"pass", "pass_with_waiver"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
