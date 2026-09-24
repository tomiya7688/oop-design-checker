#!/usr/bin/env python3
import argparse
import json
from pathlib import Path


def aggregate(results):
    unavailable = sum(item.get("status") != "ok" for item in results)
    verdicts = [
        item["verdict"]["verdict"]
        for item in results
        if item.get("status") == "ok" and item.get("verdict")
    ]
    fail_count = verdicts.count("fail")
    review_count = verdicts.count("review")

    if unavailable >= 2 or fail_count >= 2:
        verdict = "fail"
    elif unavailable == 1 or fail_count == 1 or review_count > 0:
        verdict = "review_required"
    elif len(verdicts) == 3 and all(item == "pass" for item in verdicts):
        verdict = "pass"
    else:
        verdict = "review_required"

    return {
        "verdict": verdict,
        "providerErrors": unavailable,
        "providers": [
            {
                "provider": item.get("provider"),
                "model": item.get("model"),
                "status": item.get("status"),
                "verdict": item.get("verdict", {}).get("verdict") if item.get("verdict") else None,
                "error": item.get("error"),
            }
            for item in results
        ],
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--waiver")
    args = parser.parse_args()

    paths = sorted(Path(args.input_dir).rglob("provider-result.json"))
    if len(paths) != 3:
        raise SystemExit(f"expected exactly 3 provider results, found {len(paths)}")

    results = [json.loads(path.read_text(encoding="utf-8")) for path in paths]
    output = aggregate(results)

    if args.waiver:
        waiver = json.loads(Path(args.waiver).read_text(encoding="utf-8"))
        if output["verdict"] == "review_required" and waiver.get("approved") is True and waiver.get("reason"):
            output["waiver"] = waiver
            output["verdict"] = "pass_with_waiver"

    Path(args.output).write_text(json.dumps(output, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(output, indent=2, ensure_ascii=False))
    return 0 if output["verdict"] in {"pass", "pass_with_waiver"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
