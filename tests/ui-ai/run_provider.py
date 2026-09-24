#!/usr/bin/env python3
import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_MODELS = {
    "openai": "gpt-5.6-sol",
    "gemini": "gemini-3.8-flash",
    "anthropic": "claude-opus-4-6",
}
KEY_ENV = {
    "openai": "OPENAI_API_KEY",
    "gemini": "GEMINI_API_KEY",
    "anthropic": "ANTHROPIC_API_KEY",
}


def load_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def collect_evidence(root):
    root = Path(root)
    texts = []
    images = []
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        if path.suffix.lower() == ".png" and len(images) < 16:
            images.append((relative, path))
        elif path.suffix.lower() in {".json", ".txt", ".xml"} and len(texts) < 40:
            content = path.read_text(encoding="utf-8", errors="replace")
            texts.append((relative, content[:30000]))
    return texts, images


def build_prompt(checklist, texts):
    evidence_text = "

".join(
        f"<evidence path={json.dumps(name)}>
{content}
</evidence>"
        for name, content in texts
    )
    checks = "
".join(f"- {item['id']}: {item['criterion']}" for item in checklist["checks"])
    return f"""You are one independent release UI judge.
Evaluate only the supplied evidence. Do not invent missing evidence and do not judge aesthetics beyond the checklist.
For a check that cannot be established from the supplied evidence, use verdict "review".
A blocking functional mismatch should use "fail".
Return exactly one JSON object matching the supplied schema.

Scenario: release-candidate-ui
Checklist:
{checks}

Evidence metadata:
{evidence_text}
"""


def data_url(path):
    return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def image_b64(path):
    return base64.b64encode(path.read_bytes()).decode("ascii")


def post_json(url, headers, payload, attempts=3, timeout=180):
    body = json.dumps(payload).encode("utf-8")
    last = None
    for attempt in range(attempts):
        request = urllib.request.Request(url, data=body, headers=headers, method="POST")
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return json.loads(response.read().decode("utf-8"))
        except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError) as exc:
            last = exc
            if attempt + 1 < attempts:
                time.sleep(2 ** attempt)
    raise RuntimeError(f"provider request failed after {attempts} attempts: {last}")


def extract_openai_text(response):
    if isinstance(response.get("output_text"), str):
        return response["output_text"]
    chunks = []
    for item in response.get("output", []):
        for content in item.get("content", []):
            if content.get("type") == "output_text" and isinstance(content.get("text"), str):
                chunks.append(content["text"])
    if chunks:
        return "".join(chunks)
    raise RuntimeError("OpenAI response did not contain output text")


def call_openai(model, api_key, prompt, schema, images):
    content = [{"type": "input_text", "text": prompt}]
    content.extend({"type": "input_image", "image_url": data_url(path)} for _, path in images)
    payload = {
        "model": model,
        "input": [{"role": "user", "content": content}],
        "reasoning": {"effort": "high"},
        "text": {
            "format": {
                "type": "json_schema",
                "name": "ui_release_verdict",
                "strict": True,
                "schema": schema,
            }
        },
    }
    response = post_json(
        "https://api.openai.com/v1/responses",
        {"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
        payload,
    )
    return json.loads(extract_openai_text(response))


def extract_gemini_text(response):
    if isinstance(response.get("output_text"), str):
        return response["output_text"]
    interaction = response.get("interaction")
    if isinstance(interaction, dict) and isinstance(interaction.get("output_text"), str):
        return interaction["output_text"]
    for key in ("text", "outputText"):
        if isinstance(response.get(key), str):
            return response[key]
    raise RuntimeError("Gemini response did not contain output text")


def call_gemini(model, api_key, prompt, schema, images):
    input_items = [{"type": "text", "text": prompt}]
    input_items.extend(
        {"type": "image", "mime_type": "image/png", "data": image_b64(path)}
        for _, path in images
    )
    payload = {
        "model": model,
        "input": input_items,
        "response_format": {
            "type": "text",
            "mime_type": "application/json",
            "schema": schema,
        },
    }
    response = post_json(
        "https://generativelanguage.googleapis.com/v1beta/interactions",
        {"x-goog-api-key": api_key, "Content-Type": "application/json"},
        payload,
    )
    return json.loads(extract_gemini_text(response))


def extract_anthropic_text(response):
    chunks = [
        item.get("text", "")
        for item in response.get("content", [])
        if item.get("type") == "text"
    ]
    if chunks:
        return "".join(chunks)
    raise RuntimeError("Anthropic response did not contain text")


def call_anthropic(model, api_key, prompt, schema, images):
    content = []
    for _, path in images:
        content.append(
            {
                "type": "image",
                "source": {
                    "type": "base64",
                    "media_type": "image/png",
                    "data": image_b64(path),
                },
            }
        )
    content.append({"type": "text", "text": prompt})
    payload = {
        "model": model,
        "max_tokens": 6000,
        "messages": [{"role": "user", "content": content}],
        "output_config": {
            "format": {
                "type": "json_schema",
                "schema": schema,
            }
        },
    }
    response = post_json(
        "https://api.anthropic.com/v1/messages",
        {
            "x-api-key": api_key,
            "anthropic-version": "2023-06-01",
            "Content-Type": "application/json",
        },
        payload,
    )
    return json.loads(extract_anthropic_text(response))


def validate_verdict(value, checklist):
    required = {"scenario", "verdict", "checks", "blockingIssues", "summary"}
    if set(value) != required:
        raise ValueError(f"verdict keys mismatch: {sorted(value)}")
    if value["verdict"] not in {"pass", "fail", "review"}:
        raise ValueError("invalid verdict")
    if not isinstance(value["checks"], list) or len(value["checks"]) != len(checklist["checks"]):
        raise ValueError("checks must contain every checklist item exactly once")
    expected_ids = [item["id"] for item in checklist["checks"]]
    actual_ids = [item.get("id") for item in value["checks"]]
    if actual_ids != expected_ids:
        raise ValueError(f"check IDs/order mismatch: {actual_ids}")
    for item in value["checks"]:
        if set(item) != {"id", "verdict", "evidence"}:
            raise ValueError(f"invalid check object for {item.get('id')}")
        if item["verdict"] not in {"pass", "fail", "review"}:
            raise ValueError(f"invalid check verdict for {item['id']}")
        if not isinstance(item["evidence"], str) or not item["evidence"].strip():
            raise ValueError(f"empty evidence for {item['id']}")
    if not isinstance(value["blockingIssues"], list):
        raise ValueError("blockingIssues must be an array")
    if not isinstance(value["summary"], str) or not value["summary"].strip():
        raise ValueError("summary is required")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--provider", choices=DEFAULT_MODELS, required=True)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--schema", default="tests/ui-ai/verdict.schema.json")
    parser.add_argument("--checklist", default="tests/ui-ai/checklist.json")
    args = parser.parse_args()

    schema = load_json(args.schema)
    checklist = load_json(args.checklist)
    texts, images = collect_evidence(args.evidence)
    prompt = build_prompt(checklist, texts)

    model = os.environ.get(f"UI_AI_{args.provider.upper()}_MODEL", DEFAULT_MODELS[args.provider])
    key_name = KEY_ENV[args.provider]
    api_key = os.environ.get(key_name)

    result = {
        "provider": args.provider,
        "model": model,
        "status": "error",
        "verdict": None,
        "error": None,
    }

    try:
        if not api_key:
            raise RuntimeError(f"missing required secret {key_name}")
        caller = {
            "openai": call_openai,
            "gemini": call_gemini,
            "anthropic": call_anthropic,
        }[args.provider]
        verdict = caller(model, api_key, prompt, schema, images)
        validate_verdict(verdict, checklist)
        result["status"] = "ok"
        result["verdict"] = verdict
    except Exception as exc:
        result["error"] = str(exc)

    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    Path(args.output).write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
