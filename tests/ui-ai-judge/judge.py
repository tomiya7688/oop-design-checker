#!/usr/bin/env python3
import argparse
import base64
import json
import os
import time
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--provider", choices=["openai", "google", "anthropic"], required=True)
    parser.add_argument("--scenario-dir", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--model")
    parser.add_argument("--attempts", type=int, default=3)
    return parser.parse_args()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def load_configuration(provider, model_override):
    models = read_json(ROOT / "models.json")
    config = models["providers"][provider]
    return {
        "model": model_override or config["model"],
        "secret": config["secret"],
    }


def image_data_url(path):
    mime = "image/png" if path.suffix.lower() == ".png" else "image/jpeg"
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{encoded}"


def evidence_text(scenario_dir):
    documents = []
    for name in ("expected.json", "ui-state.json", "action-log.json", "actual-diagnostics.json"):
        path = scenario_dir / name
        if path.exists():
            documents.append(
                f"<file name=\"{name}\">\n{path.read_text(encoding='utf-8-sig')}\n</file>"
            )
    return "\n".join(documents)


def build_prompt(scenario_dir):
    prompt = (ROOT / "prompt.md").read_text(encoding="utf-8")
    checklist = (ROOT / "checklist.json").read_text(encoding="utf-8")
    schema = (ROOT / "verdict.schema.json").read_text(encoding="utf-8")
    return (
        f"{prompt}\n\n"
        f"<scenario>{scenario_dir.name}</scenario>\n"
        f"<checklist>{checklist}</checklist>\n"
        f"<schema>{schema}</schema>\n"
        f"<text_evidence>{evidence_text(scenario_dir)}</text_evidence>"
    )


def images(scenario_dir):
    preferred = ("before.png", "after.png", "resized.png", "actual.png", "diff.png", "failure.png")
    found = []
    for name in preferred:
        path = scenario_dir / name
        if path.exists():
            found.append(path)
    return found[:6]


def call_openai(model, api_key, prompt, image_paths, schema):
    from openai import OpenAI

    content = [{"type": "input_text", "text": prompt}]
    content.extend(
        {
            "type": "input_image",
            "image_url": image_data_url(path),
            "detail": "high",
        }
        for path in image_paths
    )
    client = OpenAI(api_key=api_key, max_retries=0, timeout=120)
    response = client.responses.create(
        model=model,
        reasoning={"effort": "high"},
        input=[{"role": "user", "content": content}],
        text={
            "format": {
                "type": "json_schema",
                "name": "ui_release_verdict",
                "strict": True,
                "schema": schema,
            }
        },
    )
    return response.output_text


def call_google(model, api_key, prompt, image_paths, schema):
    from google import genai

    request_input = [{"type": "text", "text": prompt}]
    for path in image_paths:
        request_input.append(
            {
                "type": "image",
                "mime_type": "image/png",
                "data": base64.b64encode(path.read_bytes()).decode("ascii"),
            }
        )
    client = genai.Client(api_key=api_key)
    interaction = client.interactions.create(
        model=model,
        input=request_input,
        response_format={
            "type": "text",
            "mime_type": "application/json",
            "schema": schema,
        },
    )
    output_text = getattr(interaction, "output_text", None)
    if output_text:
        return output_text

    chunks = []
    for step in getattr(interaction, "steps", []) or []:
        if getattr(step, "type", None) != "model_output":
            continue
        for block in getattr(step, "content", []) or []:
            if getattr(block, "type", None) == "text":
                chunks.append(block.text)
    return "\n".join(chunks)


def call_anthropic(model, api_key, prompt, image_paths, schema):
    from anthropic import Anthropic

    content = []
    for path in image_paths:
        content.append(
            {
                "type": "image",
                "source": {
                    "type": "base64",
                    "media_type": "image/png",
                    "data": base64.b64encode(path.read_bytes()).decode("ascii"),
                },
            }
        )
    content.append({"type": "text", "text": prompt})
    client = Anthropic(api_key=api_key, max_retries=0, timeout=120)
    message = client.messages.create(
        model=model,
        max_tokens=8192,
        messages=[{"role": "user", "content": content}],
        tools=[
            {
                "name": "submit_ui_verdict",
                "description": "Submit the UI release verdict matching the required schema.",
                "input_schema": schema,
            }
        ],
        tool_choice={"type": "tool", "name": "submit_ui_verdict"},
    )
    for block in message.content:
        if getattr(block, "type", None) == "tool_use" and block.name == "submit_ui_verdict":
            return json.dumps(block.input)
    raise ValueError("Anthropic response did not call submit_ui_verdict.")


def parse_json_text(text):
    value = text.strip()
    fence = chr(96) * 3
    if value.startswith(fence):
        value = value.split("\n", 1)[1]
        if value.endswith(fence):
            value = value[: -len(fence)]
    return json.loads(value.strip())


def provider_schema(schema):
    sanitized = dict(schema)
    sanitized.pop("$schema", None)
    return sanitized


def validate_verdict(document, schema):
    Draft202012Validator(schema).validate(document)
    expected_ids = {f"UI-{index:03d}" for index in range(1, 13)}
    actual_ids = [item["id"] for item in document["checks"]]
    if len(actual_ids) != len(set(actual_ids)) or set(actual_ids) != expected_ids:
        raise ValueError("Verdict checks must contain UI-001 through UI-012 exactly once.")


def write_result(path, provider, model, status, result=None, error=None):
    document = {
        "provider": provider,
        "model": model,
        "status": status,
        "error": error,
        "result": result,
    }
    output = Path(path)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(document, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def main():
    args = parse_args()
    scenario_dir = Path(args.scenario_dir).resolve()
    config = load_configuration(args.provider, args.model)
    model = config["model"]
    api_key = os.environ.get(config["secret"])
    if not api_key:
        write_result(
            args.output,
            args.provider,
            model,
            "error",
            error=f"Missing required secret {config['secret']}.",
        )
        return 0

    schema = read_json(ROOT / "verdict.schema.json")
    request_schema = provider_schema(schema)
    prompt = build_prompt(scenario_dir)
    image_paths = images(scenario_dir)
    callers = {
        "openai": call_openai,
        "google": call_google,
        "anthropic": call_anthropic,
    }

    last_error = None
    for attempt in range(1, max(1, args.attempts) + 1):
        try:
            text = callers[args.provider](
                model,
                api_key,
                prompt,
                image_paths,
                request_schema,
            )
            document = parse_json_text(text)
            validate_verdict(document, schema)
            if document["scenario"] != scenario_dir.name:
                raise ValueError(
                    f"Scenario mismatch: expected {scenario_dir.name}, found {document['scenario']}."
                )
            write_result(args.output, args.provider, model, "ok", result=document)
            return 0
        except Exception as exc:
            last_error = f"{type(exc).__name__}: {exc}"
            if attempt < max(1, args.attempts):
                time.sleep(min(8, 2**attempt))

    write_result(args.output, args.provider, model, "error", error=last_error)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
