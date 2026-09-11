# Vector NPL research harness

This directory contains research-only Python tooling for the Vector NPL semantic-recognition proof. Normal Vector builds and execution remain C#/.NET-only and do not require Python.

P03 provides a model-independent harness only. It does not download or call a pretrained model, train anything, generate Vector source, or execute Vector code.

## Requirements and setup

Use Python 3.11 or newer. From the repository root in Windows PowerShell:

```powershell
cd .\research\npl
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -e .
```

P03 has no runtime dependencies outside the Python standard library. The editable installation only installs this local package and its console entry point.

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
