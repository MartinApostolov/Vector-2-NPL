# NPL-P04 — Comparison Training/Development Dataset Design

**Status:** authoritative input package for NPL-P04  
**Language:** English  
**Model-output contract:** P03 v1  
**Scope:** data + deterministic validation only; no model training

## 1. Purpose

P04 freezes the data used for comparison-model development before P05/P06 experiments. It is deliberately separate from the P02 proof benchmark.

The data teaches and tests exact distinctions among:

```text
GT
GTE
LT
LTE
EQ
NEQ
REJECT
```

The target is the strict P03 model-output envelope, never Vector source code.

## 2. Split architecture

There are now four conceptually different corpora:

```text
P04 comparison_training.jsonl
  -> may be used to fine-tune in P06

P04 comparison_training_development.jsonl
  -> visible training-development holdout; may be evaluated repeatedly

P02 comparison_development.jsonl
  -> visible external/adversarial development benchmark

P02 comparison_gate_sealed.jsonl
  -> sealed final comparison gate; DO NOT inspect/use for P04-P06 decisions
```

P04 was constructed without consulting the contents of the P02 sealed gate. Do not add a sealed-gate overlap check that reads individual sealed cases during P04. Preserving the seal is more important than attempting post-hoc similarity optimization.

## 3. Frozen sizes

Training: **1260** cases

```json
{
  "EQ": 180,
  "GT": 180,
  "GTE": 180,
  "LT": 180,
  "LTE": 180,
  "NEQ": 180,
  "REJECT": 180
}
```

Training-development: **210** cases

```json
{
  "EQ": 30,
  "GT": 30,
  "GTE": 30,
  "LT": 30,
  "LTE": 30,
  "NEQ": 30,
  "REJECT": 30
}
```

Each semantic class is balanced within a split. REJECT is balanced with each semantic class.

## 4. Record schema

Semantic example:

```json
{
  "id": "p04-train-gte-01-01",
  "language": "en",
  "split": "train",
  "input": "score must be greater than or equal to 13.",
  "expectation": "semantic",
  "expected": {
    "type": "comparison",
    "kind": "GTE",
    "left": {"type":"identifier","name":"score"},
    "right": {"type":"numberLiteral","value":13}
  },
  "targetOutput": "{\"outcome\":\"semantic\",...}",
  "family": "gte-train-01",
  "tags": ["GTE","numeric","hard-negative-pair"],
  "pair": "train-upper-01-01"
}
```

Reject example:

```json
{
  "id": "p04-train-reject-01-01",
  "language": "en",
  "split": "train",
  "input": "signal is approximately 10.",
  "expectation": "reject",
  "rejectReason": "ambiguous-approximation",
  "targetOutput": "{\"outcome\":\"reject\"}",
  "family": "reject-approx-eq-train",
  "tags": ["REJECT","approx-eq"]
}
```

Allowed top-level members are exactly:

```text
id
language
split
input
expectation
expected            (semantic only)
rejectReason        (reject only)
targetOutput
family
tags
pair                (optional; semantic hard-negative grouping)
```

## 5. Target contract

Every `targetOutput` must parse through the real P03 `parse_model_output`.

Semantic records must target exactly:

```json
{"outcome":"semantic","semantic":<the exact expected tree>}
```

Reject records must target exactly:

```json
{"outcome":"reject"}
```

The validator must prove semantic `targetOutput` and `expected` describe the same tree. It must also require the frozen target string to be the canonical compact JSON form used by P04 (UTF-8 text, no insignificant whitespace, `outcome` before `semantic`, and the P01 semantic member order produced by the P03 `semantic_to_data` representation). Reject targets must be exactly `{"outcome":"reject"}`. This prevents target-format noise from becoming part of fine-tuning.

## 6. Hard-negative design

The dataset intentionally uses paired examples where the subject/literal stay fixed and the semantic boundary changes.

Pair families cover:

```text
GT  vs GTE
LT  vs LTE
EQ  vs NEQ
```

This is deliberate. The previous failure mode we are targeting is collapse of near-neighbor semantics.

Do not randomly split members of the same phrase family between train and development. P04 development uses phrase families held out from training.

Pair integrity is part of validation: every semantic P04 record has a nonblank `pair`; each pair id appears exactly twice within its split; the paired kinds must be exactly one of `{GT,GTE}`, `{LT,LTE}`, or `{EQ,NEQ}`; and the two cases must have identical semantic operands. This guarantees the hard-negative pairing cannot silently drift.

## 7. Coverage

Training covers:

- strict and inclusive numeric boundaries;
- direct wording;
- reversed wording;
- explicit boundary-inclusion/exclusion wording;
- number-line wording;
- positive, zero, negative, integer, and decimal literals;
- varied identifiers;
- `CurrentItem`;
- numeric EQ/NEQ;
- text EQ/NEQ;
- boolean EQ/NEQ;
- ambiguity/approximation rejection;
- uncertainty/modality rejection;
- missing-threshold rejection;
- fuzzy text similarity rejection;
- fuzzy boolean rejection.

The P04 development split uses separately authored phrase families and mostly separate identifier/literal pools.

## 8. Leakage rules

P04 validation must fail on:

- duplicate ids within or across P04 splits;
- duplicate normalized inputs within or across P04 splits;
- a phrase-family name appearing in both P04 splits;
- semantic placeholder-template overlap between P04 training and P04 development;
- exact input overlap with the visible P02 development benchmark;
- trivial semantic placeholder-template overlap with the visible P02 development benchmark.

Do **not** load the P02 sealed gate for similarity/leakage analysis in P04.

The sealed gate may only be mechanically protected by its existing P02/P03 policy. It must not influence P04 data edits.

## 9. Validation requirements

Implement a deterministic P04 dataset loader/validator under the research Python package. Reuse P03 strict JSON/Semantic IR/model-output code; do not create a competing parser.

Reject at least:

- malformed JSONL;
- duplicate JSON members;
- unknown top-level fields;
- missing/blank required strings;
- wrong language;
- wrong split value for a file;
- invalid expectation;
- missing/empty/blank tags;
- missing/blank family;
- semantic record without `expected`;
- semantic record with `rejectReason`;
- reject record with `expected`;
- reject record without `rejectReason`;
- invalid `expected` Semantic IR;
- non-comparison expected root;
- invalid `targetOutput`;
- mismatch between `expected` and semantic `targetOutput`;
- semantic target for a reject example;
- reject target for a semantic example;
- malformed optional `pair`;
- semantic record without a pair;
- pair id not appearing exactly twice in its split;
- pair whose operator kinds are not a critical near-neighbor pair;
- pair whose left/right operands differ;
- non-canonical `targetOutput` string even if it parses to the right meaning;
- manifest count/hash/class mismatch.

## 10. Manifest

`dataset_manifest.json` freezes canonical SHA-256 hashes using the same cross-platform text canonicalization contract as P02/P03.

Training canonical SHA-256:

```text
32c44fbb4c29618c9b1e680a9140a9ae1d55efb81d9e173dcce04f364fff1b9d
```

Development canonical SHA-256:

```text
ac1ffd374434aead0ed635fa57f54730fab4a879ba7d623ab20ad19248c2a42f
```

Do not silently regenerate or relabel frozen examples if validation fails. Report the specific case.

## 11. P04 implementation scope

Codex should add:

```text
research/npl/datasets/comparisons/
  README.md
  comparison_training.jsonl
  comparison_training_development.jsonl
  dataset_manifest.json
```

and Python loader/validator/leak-analysis support plus unit tests inside the existing P03 package.

P04 may add CLI validation/summary commands if useful, but must not add model inference/training.

## 12. Non-goals

P04 must not:

- install Transformers/PyTorch;
- download FLAN-T5/mT5;
- train/fine-tune;
- run zero/few-shot inference;
- use the sealed P02 gate;
- generate Vector source;
- modify C# runtime/CLI/editor behavior.

## 13. Completion criteria

P04 is complete when:

1. both frozen data files are copied exactly into the repository;
2. manifest hashes/counts verify;
3. all target outputs pass the P03 strict contract;
4. semantic targets exactly match `expected`;
5. P04 train/dev phrase families are disjoint;
6. P04 train/dev exact/template leakage checks pass;
7. visible P02 development exact/template checks pass;
8. no P02 sealed-gate content is loaded for P04 analysis;
9. Python tests pass;
10. P03 fixture still passes;
11. C# tests still pass;
12. no model work has begun.
