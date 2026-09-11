# Vector NPL research harness

This directory contains research-only Python tooling for the Vector NPL semantic-recognition proof. Normal Vector builds and execution remain C#/.NET-only and do not require Python.

P03 and P04 provide model-independent harness and data tooling. P05 adds an optional, research-only inference baseline. None of these phases train a model, generate Vector source, execute Vector code, or change the Vector runtime.

## Requirements and setup

Use Python 3.11 or newer. From the repository root in Windows PowerShell:

```powershell
cd .\research\npl
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -e .
```

The base research package has no runtime dependencies outside the Python standard library. The editable installation only installs this local package and its console entry point.

## Run the unit tests

With the virtual environment active:

```powershell
python -m unittest discover -s tests -p "test_*.py"
```

## Validate raw model output

Validate an explicit rejection:

```powershell
python -m vector_npl_research validate-output --text '{"outcome":"reject"}'
```

Validate text stored in a file:

```powershell
python -m vector_npl_research validate-output --file .\output.txt
```

The command classifies input as `VALID_SEMANTIC`, `VALID_REJECT`, or `INVALID_MODEL_OUTPUT`. Malformed output is never treated as an explicit rejection.

## Score manual development predictions

Create a prediction JSONL file containing exactly one record per development case:

```json
{"caseId":"cmp-dev-001","rawOutput":"{\"outcome\":\"semantic\",\"semantic\":{\"type\":\"comparison\",\"kind\":\"GT\",\"left\":{\"type\":\"identifier\",\"name\":\"score\"},\"right\":{\"type\":\"numberLiteral\",\"value\":10}}}"}
```

Then run:

```powershell
python -m vector_npl_research score `
  --benchmark .\benchmarks\comparisons\comparison_development.jsonl `
  --manifest .\benchmarks\comparisons\benchmark_manifest.json `
  --predictions .\predictions.jsonl `
  --output-dir .\runs\manual-development `
  --model-id manual-predictions `
  --seed 1234
```

The harness verifies the selected dataset's canonical SHA-256 and manifest role before scoring. Prediction record order does not affect the result.

## Run the synthetic fixture

The fixture uses a small synthetic development benchmark and a fixed predictor with outputs written independently of the expected records:

```powershell
python -m vector_npl_research run-fixture
```

To choose an output directory:

```powershell
python -m vector_npl_research run-fixture --output-dir .\runs\fixture-review --seed 1234
```

## Sealed-gate protection

The real `comparison_gate_sealed.jsonl` dataset is frozen for P07. The harness refuses to run or score any manifest entry with role `sealed-gate` by default.

The CLI exposes `--allow-sealed-gate` solely as an explicit future-P07 escape hatch. Do not use it against the real gate during P03-P06. P03 tests exercise this behavior only with synthetic data.

## Run artifacts

Each successful score or fixture run creates:

```text
<output-dir>/
  run.json
  predictions.jsonl
  results.jsonl
  summary.json
```

`run.json` records the Git commit, dirty state, Python/platform information, benchmark identity and hash, model metadata, seed, and parameters. Raw predictor output is preserved in both prediction and per-case result records.

Generated directories beneath `research/npl/runs/` are intentionally ignored by Git. Benchmark source data, Python source, test data, and tests remain tracked.

## P05 frozen FLAN-T5-small baseline

P05 evaluates the unchanged pretrained `google/flan-t5-small` model at revision `0fc9ddf78a1e988dac52e2dac162b0ede4fd74ab` with the frozen `p05-comparison-v3` prompt. It is inference/evaluation-only and remains research-only. Its PyTorch and Hugging Face packages are isolated in the optional `p05` extra, so ordinary P03/P04 commands and tests do not require them.

From the repository root, create the existing local virtual-environment convention and install the optional frozen dependencies with the environment's Python explicitly:

```powershell
cd .\research\npl
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -e ".[p05]"
```

P05 separates network access from evaluation. Download/cache the exact model and tokenizer revision once:

```powershell
.\.venv\Scripts\python.exe -m vector_npl_research cache-p05-model
```

The baseline commands subsequently use the local Hugging Face cache only and fail with an actionable message if the frozen revision is unavailable. They always use CPU, float32, SafeTensors, `trust_remote_code=false`, and one deterministic greedy generation with `do_sample=false`, `num_beams=1`, and `max_new_tokens=128`. Before model weights are loaded or any prediction is generated, a tokenizer-only whole-dataset preflight renders every selected case without truncation. If any rendered prompt exceeds 512 tokens, the run fails with the case ID, token count, condition, and dataset; prompts are never truncated.

P05 has exactly two conditions. `zero-shot` has no demonstrations. `few-shot-2` uses exactly these P04 training records, in this order:

```text
p04-train-gt-01-01
p04-train-gte-01-01
```

Their input text and canonical target output are loaded through the authoritative P04 loader. The prompt is not adapted from model failures or evaluation results.

Run the four canonical baselines from `research\npl` with the local environment's Python:

```powershell
.\.venv\Scripts\python.exe -m vector_npl_research run-p05-baseline `
  --condition zero-shot `
  --dataset p04-development `
  --output-dir .\runs\p05-zero-p04-development `
  --seed 1234

.\.venv\Scripts\python.exe -m vector_npl_research run-p05-baseline `
  --condition few-shot-2 `
  --dataset p04-development `
  --output-dir .\runs\p05-few2-p04-development `
  --seed 1234

.\.venv\Scripts\python.exe -m vector_npl_research run-p05-baseline `
  --condition zero-shot `
  --dataset p02-development `
  --output-dir .\runs\p05-zero-p02-development `
  --seed 1234

.\.venv\Scripts\python.exe -m vector_npl_research run-p05-baseline `
  --condition few-shot-2 `
  --dataset p02-development `
  --output-dir .\runs\p05-few2-p02-development `
  --seed 1234
```

Only the P04 training-development split and visible P02 development split are selectable. The P02 sealed gate remains forbidden until P07 and has no P05 alias or override. The exact decoded model text is stored and scored as-is: P05 performs no stripping, prose/Markdown extraction, retries, or JSON repair. Each run reuses the P03 artifacts and writes `run.json`, `predictions.jsonl`, `results.jsonl`, and `summary.json` beneath `research/npl/runs/`.
