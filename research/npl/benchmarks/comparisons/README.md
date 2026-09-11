# NPL-P02 — English Comparison Benchmark Design

**Status:** Input package for implementation review  
**Proof stage:** NPL-P02 only  
**Semantic contract:** `Vector.Npl.Semantics` from NPL-P01  
**Language:** English

## 1. Purpose

This benchmark tests whether an NPL recognizer can distinguish exact comparison meanings before any Vector source generation exists.

The critical supported classes are:

```text
GT
GTE
LT
LTE
EQ
NEQ
```

The benchmark also contains deliberately ambiguous or unsupported requests whose expected outcome is rejection.

## 2. Corpus split

Two files are intentionally separated.

### `comparison_development.jsonl`

Visible development/adversarial material.

It may be used in P05/P06 to:
- inspect model failures;
- compare prompts/configurations;
- design training data;
- tune model/training choices.

It must never later be described as an untouched final holdout.

Case count: **60**

Class counts:

```text
{
  "EQ": 6,
  "GT": 8,
  "GTE": 10,
  "LT": 8,
  "LTE": 10,
  "NEQ": 6,
  "REJECT": 12
}
```

### `comparison_gate_sealed.jsonl`

Frozen P07 gate material.

Before P07:
- do not use its cases as training examples;
- do not use its cases as few-shot demonstrations;
- do not use its individual outputs/failures to tune prompts, thresholds, datasets, or training;
- do not regenerate or edit the file because a model performs badly on it.

Case count: **72**

Class counts:

```text
{
  "EQ": 10,
  "GT": 10,
  "GTE": 10,
  "LT": 10,
  "LTE": 10,
  "NEQ": 10,
  "REJECT": 12
}
```

The SHA-256 is recorded in `benchmark_manifest.json`. Hashing uses canonical UTF-8 text: an optional BOM is ignored, CRLF/CR are normalized to LF, and the content ends with exactly one LF. This makes the freeze check stable on Windows checkouts.

If P07 fails and its individual failures are inspected to improve the recognizer, this gate becomes development evidence. A new independently authored sealed gate is then required before claiming a later unbiased PASS.

## 3. JSONL schema

Every line is one JSON object.

### Supported semantic case

```json
{
  "id": "cmp-dev-001",
  "language": "en",
  "input": "score is more than 10.",
  "expectation": "semantic",
  "expected": {
    "type": "comparison",
    "kind": "GT",
    "left": {
      "type": "identifier",
      "name": "score"
    },
    "right": {
      "type": "numberLiteral",
      "value": 10
    }
  },
  "tags": ["GT", "strict", "minimal-pair"],
  "pair": "dev-p01"
}
```

`expected` must deserialize through the real P01 `SemanticJson` contract and pass `SemanticValidator`.

### Rejection case

```json
{
  "id": "cmp-dev-r01",
  "language": "en",
  "input": "score is around 10.",
  "expectation": "reject",
  "rejectReason": "ambiguous-approximation",
  "tags": ["reject", "ambiguous", "approximate"]
}
```

A rejection expectation is benchmark metadata, not a new Semantic IR node.

## 4. Required loader/schema checks

P02 tooling must reject benchmark files containing:

- malformed JSON;
- duplicate case ids;
- missing or blank ids;
- unsupported language values for this benchmark;
- missing or blank input text;
- unknown top-level members;
- unsupported `expectation` values;
- `semantic` cases without `expected`;
- `reject` cases with `expected`;
- `reject` cases without a nonblank `rejectReason`;
- expected Semantic IR that cannot be parsed by `SemanticJson`;
- expected Semantic IR that fails `SemanticValidator`;
- empty tags or blank tag values;
- exact duplicate normalized inputs within a split.

The implementation should keep the dataset schema outside the Semantic IR project if practical; benchmark metadata is research/test data, not language semantics.

## 5. Cross-split leakage checks

At minimum, tooling must report/fail on:

1. identical case ids across development and gate;
2. exact duplicate input text after deterministic normalization;
3. exact duplicate `(normalized input, expected disposition)` records.

Also implement a **template leakage diagnostic** that normalizes obvious replaceable surface values, at least:
- numeric literals;
- quoted text literals;
- boolean literals;
- identifier names when they can be reliably identified from the expected IR.

This diagnostic may initially report suspicious overlaps rather than automatically fail every fuzzy match. Its purpose is to reveal accidental template cloning without creating a complicated NLP subsystem inside P02.

Do not build embeddings or model-based similarity detection for leakage.

## 6. Gate scoring contract

P02 does not implement model evaluation yet, but the benchmark freezes the later scoring rule.

For supported semantic cases:
- exact semantic-tree match is required;
- wrong comparison kind is a failure;
- wrong identifier is a failure;
- wrong literal/value is a failure;
- invalid model output is a failure;
- rejection of a supported, unambiguous case is a failure.

For rejection cases:
- producing a precise supported semantic tree is a failure;
- an explicit reject/unsupported outcome is a pass.

Critical P07 requirement on the sealed gate:

```text
GT  <-> GTE : zero semantic-confusion errors
LT  <-> LTE : zero semantic-confusion errors
EQ  <-> NEQ : zero semantic-confusion errors
```

This zero-confusion rule is in addition to exact semantic matching; it is not a replacement for it.

## 7. Coverage intentionally represented

The cases include:
- strict vs inclusive upper/lower comparisons;
- minimal pairs in development;
- varied paraphrases in the gate;
- direct and reversed wording;
- negated formulations;
- zero;
- negative numbers;
- decimal values;
- varied identifier names;
- numeric equality/inequality;
- text equality/inequality;
- boolean equality/inequality;
- `CurrentItem`;
- filler wording;
- intentionally ambiguous approximations;
- vague/implicit thresholds;
- unsupported similarity/fuzzy meanings.

The gate avoids simply copying the visible development sentences.

## 8. Non-goals

P02 must not add:
- FLAN-T5 or mT5;
- Python model tooling;
- training data generation;
- inference;
- confidence thresholds;
- Vector source generation;
- execution;
- CLI or IDE NPL integration.

The only implementation work in P02 is benchmark data plus deterministic loading, schema validation, immutability checks, and leakage/deduplication checks.
