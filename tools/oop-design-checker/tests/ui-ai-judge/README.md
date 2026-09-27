# UI AI Judge Panel

This directory defines the independent OpenAI, Google, and Anthropic vision review used by the 1.0.0 GUI release gate.

## Defaults

- OpenAI: `gpt-5.6-sol` via `OPENAI_API_KEY`
- Google: `gemini-3.8-flash` via `GEMINI_API_KEY`
- Anthropic: `claude-opus-4-6` via `ANTHROPIC_API_KEY`

Model IDs are stored in `models.json` so they can be changed without rewriting the harness.

## Contract

Every provider receives the same:

- `prompt.md`
- `checklist.json`
- `verdict.schema.json`
- scenario evidence files and screenshots

Provider output is schema-validated locally. Missing secrets, timeouts, provider errors, malformed JSON, or schema violations are recorded as provider errors and are never converted into PASS.

## Consensus

`aggregate.py` enforces:

- 3/3 PASS -> PASS
- two or more FAIL -> FAIL
- one FAIL or REVIEW -> REVIEW_REQUIRED
- one provider unavailable -> REVIEW_REQUIRED
- two or more providers unavailable -> FAIL

A waiver can only resolve REVIEW_REQUIRED and must record `scenario`, `reason`, and `approvedBy`. FAIL cannot be waived by the aggregator.

## Deterministic tests

`test_aggregate.py` and `test_judge.py` do not call external providers. They test the schema, outage policy, disagreement handling, and waiver rules. Real provider calls are performed only by the UI AI workflow with GitHub Actions secrets.


## Workflow

`.github/workflows/ui-ai-panel.yml` has two modes:

- pull request: runs only deterministic contract tests; no provider API is called
- manual/release-candidate: provide the trusted UI evidence workflow run ID, artifact name, and scenario ID; the workflow downloads that evidence, calls all three providers independently, then aggregates their results

The provider jobs always persist a result envelope. Missing secrets or API failures become `status: error`, not a passing verdict. The aggregate job then applies the outage/disagreement policy above.
