# NPL-P05 — FLAN-T5 Zero/Few-Shot Baseline Results

## Purpose

P05 evaluates whether an unchanged pretrained semantic-capable model can map English comparison requests directly into Vector NPL's strict P03 semantic-output contract without fine-tuning, output repair, reranking, confidence thresholds, or deterministic post-processing.

This phase is a baseline experiment.

A poor model result does not invalidate the P05 harness. P05 is intended to establish what the selected pretrained model can do before supervised adaptation in a later phase.

P05 does not use the sealed P02 final gate.

## Frozen model

Model:

```text
google/flan-t5-small
```

Frozen Hugging Face revision:

```text
0fc9ddf78a1e988dac52e2dac162b0ede4fd74ab
```

Canonical execution settings:

```text
device: cpu
dtype: float32
useSafetensors: true
trustRemoteCode: false
```

The model is used without fine-tuning.

## Frozen prompt

Prompt version:

```text
p05-comparison-v3
```

Prompt file:

```text
research/npl/prompts/comparison_p05_v3.txt
```

Canonical prompt SHA-256:

```text
743951694521965ff2d6c1de374057290ee3b2a918afa8fafaccedf9e5ad9987
```

P05 originally exposed mechanical prompt-length problems during local acceptance. Earlier uncommitted prompt revisions were shortened before the final baseline because their few-shot forms exceeded the frozen FLAN-T5 512-token input limit.

The final v3 prompt was preflighted with the real frozen tokenizer across all four canonical run combinations.

Maximum rendered input-token counts were:

| Condition  | Dataset                  | Maximum tokens | Margin below 512 |
| ---------- | ------------------------ | -------------: | ---------------: |
| zero-shot  | P04 training-development |            292 |              220 |
| few-shot-2 | P04 training-development |            485 |               27 |
| zero-shot  | P02 visible development  |            281 |              231 |
| few-shot-2 | P02 visible development  |            474 |               38 |

No prompt was truncated.

## Frozen decoding

The model receives exactly one deterministic generation attempt per case:

```text
doSample: false
numBeams: 1
maxNewTokens: 128
randomSeed: 1234
```

There is no:

* sampling;
* temperature;
* top-p or top-k selection;
* output retry;
* JSON repair;
* Markdown stripping;
* brace extraction;
* confidence threshold;
* operator reranking;
* deterministic semantic correction.

The decoded model output is preserved and passed directly to the strict P03 model-output parser.

## Prompt conditions

P05 evaluates two frozen prompt conditions.

### zero-shot

No demonstrations are supplied.

### few-shot-2

Exactly two demonstrations are used, in this order:

```text
p04-train-gt-01-01
p04-train-gte-01-01
```

The authoritative request and target output for those demonstrations are loaded from:

```text
research/npl/datasets/comparisons/comparison_training.jsonl
```

Training-dataset SHA-256:

```text
32c44fbb4c29618c9b1e680a9140a9ae1d55efb81d9e173dcce04f364fff1b9d
```

These two examples were selected before the baseline results were inspected. They form a minimal hard-negative pair for strict `GT` versus inclusive `GTE`.

No demonstrations were added after observing results.

## Evaluation datasets

### P04 training-development

File:

```text
comparison_training_development.jsonl
```

Cases:

```text
210
```

SHA-256:

```text
ac1ffd374434aead0ed635fa57f54730fab4a879ba7d623ab20ad19248c2a42f
```

The dataset contains balanced examples for:

```text
GT
GTE
LT
LTE
EQ
NEQ
REJECT
```

### P02 visible development

File:

```text
comparison_development.jsonl
```

Cases:

```text
60
```

SHA-256:

```text
42b64a251fb842b541f42921b5137c4956d92c7a7fd8cfb1ee6d596b568f15b7
```

This is the visible P02 development benchmark.

The sealed P02 final gate was not evaluated, inspected, used for demonstrations, used for prompt selection, or used for decoding selection.

## Canonical baseline results

| Condition  | Dataset                  | Passed | Failed | Invalid model outputs |
| ---------- | ------------------------ | -----: | -----: | --------------------: |
| zero-shot  | P04 training-development |      0 |    210 |                   210 |
| few-shot-2 | P04 training-development |      0 |    210 |                   210 |
| zero-shot  | P02 visible development  |      0 |     60 |                    60 |
| few-shot-2 | P02 visible development  |      0 |     60 |                    60 |

Across the four canonical P05 runs:

```text
total evaluated cases: 540
valid P03 semantic/reject outputs: 0
INVALID_MODEL_OUTPUT results: 540
```

Therefore:

```text
exact semantic passes: 0
expected reject passes: 0
```

in every run.

## Interpretation of critical-confusion counts

The P03 summaries report:

```text
GT <-> GTE: 0
LT <-> LTE: 0
EQ <-> NEQ: 0
```

for these runs.

These values must **not** be interpreted as successful operator discrimination.

The model never produced a valid P03 semantic object, so the scorer never reached the semantic-comparison stage required to classify a valid output as an operator confusion.

The correct interpretation is:

```text
operator-level performance was not measurable because output-contract compliance was 0%.
```

## Observed failure modes

The dominant failure was failure to emit the required P03 JSON envelope.

Representative behaviors included:

### Natural-language paraphrase instead of semantic JSON

Examples included outputs conceptually equivalent to:

```text
count exceeds 7.
status is not "ready".
the current item equals 8.
```

These may retain part of the input meaning, but they are invalid under the strict P03 model-output contract.

### Copying or partially reproducing prompt-schema fragments

The model sometimes emitted fragments resembling:

```text
"outcome":"semantic"
"value":NUMBER
"type":"comparison"
```

without producing a syntactically and structurally valid semantic object.

### Literal-only output

Some cases resulted in values such as:

```text
0
4.5
73
```

without any semantic envelope.

### Degenerate or repetitive text

Some outputs contained repeated fragments or malformed text unrelated to a valid P03 object.

### Failure to emit explicit rejection

Ambiguous and unsupported cases also failed to produce:

```json
{"outcome":"reject"}
```

They instead produced malformed output or natural-language text.

## Zero-shot versus few-shot

The two GT/GTE demonstrations changed some surface-generation behavior but did not improve strict output-contract compliance.

Both conditions achieved:

```text
0 valid outputs
```

on both evaluated development datasets.

Therefore, the P05 evidence does not support continuing prompt-only optimization of this frozen FLAN-T5-small baseline.

## Harness validation

The result should not be interpreted as a failure of the P03 parser or scorer.

The P03 synthetic regression fixture continued to pass:

```text
3 / 3
```

including:

```text
valid GT semantic output
valid GTE semantic output
valid explicit reject output
```

This confirms that valid P03 outputs are accepted and scored correctly when supplied.

The P05 implementation also passed its research test suite and the existing Vector C# regression suites before the real baseline runs.

## P05 conclusion

The frozen `google/flan-t5-small` model, used without fine-tuning, is not capable of reliably producing the strict P03 semantic-output contract under either the frozen zero-shot or two-example few-shot condition.

The baseline failed before meaningful operator-level comparison could be measured:

```text
540 / 540 evaluated outputs were INVALID_MODEL_OUTPUT.
```

This is still a successful P05 experiment because it establishes a reproducible baseline and identifies the principal failure mode:

```text
output-contract learning / structured semantic generation
```

must be taught rather than assumed from generic pretrained instruction-following ability.

The result provides the experimental motivation for a later supervised-adaptation phase.

It does **not** justify weakening the strict P03 contract, adding JSON repair, silently accepting prose, or using deterministic post-processing to convert natural-language outputs into semantic IR.

Those changes would measure a different architecture.

## Boundary after P05

P05 ends here.

No P06 fine-tuning or training work is included in this phase.

The sealed P02 final gate remains reserved for P07.
