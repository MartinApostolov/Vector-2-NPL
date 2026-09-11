# Vector NPL Semantic Proof Architecture

**Status:** NPL-P00 proof architecture  
**Repository:** Vector-2-NPL  
**Proof branch:** `npl-semantic-proof`  
**Base:** `main` at `53132379a775f64896e999576add96c2d083e629`  
**Scope:** Semantic-recognition proof only; no Natural-language → Vector source generation

## 1. Purpose

The NPL proof phase exists to answer one question before any code-generation feature is built:

> Can a pretrained language model, after focused Vector-specific training, reliably convert genuinely varied natural-language requests into exact, typed, language-independent semantics without collapsing programmatically important near-neighbour meanings?

The proof is specifically intended to expose failures such as:

```text
"more than 10"   -> GT
"10 or more"     -> GTE
```

being confused.

A previous general-purpose-model experiment showed that semantically close operators could receive nearly identical confidence. The new architecture therefore treats exact semantic recognition as a hard prerequisite rather than assuming that code generation will hide or repair recognition mistakes.

## 2. Proof architecture

The proof-stage pipeline is:

```text
Natural-language request
        |
        v
Pretrained semantic-capable model
        |
        v
Typed, Vector-independent Semantic IR
        |
        v
Deterministic validation
        |
        v
Evaluation / rejection decision
        |
        v
HARD GO / NO-GO GATE
```

Only after the complete semantic-recognition proof passes may a later phase add:

```text
Validated Semantic IR
        |
        v
Deterministic Vector source generation
        |
        v
Vector.Analysis validation
        |
        v
Preview / explicit execution
```

The model must not produce Vector source during the proof phase.

## 3. Hard no-code-generation boundary

During NPL-P00 through NPL-P13:

- no Natural-language → Vector source generator is implemented;
- no generated Vector source is executed;
- no automatic NPL execution path is added;
- no IDE NPL command is added;
- no phrase-table or dictionary-based code generator is added;
- no embedding-nearest-operator system is used as a hidden fallback;
- no existing interpreter, VM, language-server, Visual Studio, or VS Code behavior is changed unless a later approved proof commit explicitly requires isolated NPL support infrastructure;
- Python research tooling must not become a normal Vector runtime dependency.

The proof phase measures semantic recognition only.

## 4. Semantic boundary

The model produces semantic concepts, not Vector syntax.

Conceptually:

```text
"score is at least 10"
        |
        v
Comparison(
    kind = GTE,
    left = Identifier("score"),
    right = NumberLiteral(10)
)
```

The proof representation uses semantic names such as:

```text
GT
GTE
LT
LTE
EQ
NEQ
```

and not Vector tokens such as:

```text
>
>=
<
<=
==
!=
```

The exact C# Semantic IR types, serialization, and validation contract are intentionally deferred to NPL-P01. NPL-P00 freezes the architectural boundary, not the implementation details of that IR.

## 5. Initial proof concepts

The first proof surface is intentionally narrow.

### Comparison meanings

```text
GT
GTE
LT
LTE
EQ
NEQ
```

### Operand meanings

The first Semantic IR design is expected to support concepts equivalent to:

```text
Identifier
NumberLiteral
TextLiteral
BooleanLiteral
CurrentItem
```

These are architectural targets for P01, not implementation added by P00.

The proof must deliberately emphasize near-neighbour distinctions, especially:

```text
GT  vs GTE
LT  vs LTE
EQ  vs NEQ
```

as well as negated formulations whose surface wording differs from the resulting semantic relation.

## 6. Model candidates

The initial model strategy is to reuse pretrained language understanding rather than train a general-purpose language model from scratch.

Initial candidates:

1. `FLAN-T5-small`
2. `mT5-small`
3. an encoder/classifier-style alternative later, if generative semantic parsing remains unreliable

Training a general-purpose model from scratch is not part of the initial proof plan.

Model selection must be driven by the frozen evaluation protocol rather than by convenience or by performance on training examples.

## 7. Dataset separation and leakage rules

The proof uses separate data roles.

### Training set

Used to update model weights.

It may contain:

- ordinary examples;
- paraphrases;
- hard-negative minimal pairs;
- varied identifiers and literals;
- negation;
- filler wording;
- controlled grammatical variation.

### Development/adversarial set

Used during P05/P06 experimentation to inspect failures, compare prompts/configurations, tune training choices, and decide what experiment to run next.

Once an example has influenced a model, prompt, threshold, objective, dataset generator, or experiment decision, it belongs to development evidence and must not be treated as an untouched final holdout.

### Sealed gate holdout

Used only for the hard gate after the candidate model and evaluation procedure are frozen.

Rules:

- it is not training data;
- it is not development data;
- its individual examples and failures are not used to design the candidate being tested;
- it is deduplicated against training and development data;
- obvious template-equivalent leakage must also be checked, not only byte-for-byte duplicates;
- the model/configuration evaluated at the gate is fixed before the sealed set is opened;
- if the sealed gate is failed and its failures are then used to improve the system, that set becomes development evidence and a new sealed gate set is required for a later unbiased gate.

This separation prevents repeated inspection of the final gate from becoming hidden test-set overfitting.

## 8. Comparison hard gate

The first critical gate is comparison recognition.

For the sealed adversarial comparison holdout:

```text
GT  <-> GTE : zero semantic-confusion errors
LT  <-> LTE : zero semantic-confusion errors
EQ  <-> NEQ : zero semantic-confusion errors
```

Additional requirements:

- numeric literals are preserved exactly;
- identifiers are preserved exactly;
- malformed model output is rejected by deterministic validation;
- ambiguous or unsupported input is not silently converted into a precise supported meaning;
- every supported, unambiguous gate example must produce the correct valid semantics.

Rejection is **not** a loophole for passing the gate. Rejecting a supported, unambiguous example counts as a failure. Conversely, forcing an intentionally ambiguous/unsupported example into a precise semantic class also counts as a failure.

The exact benchmark size and composition are defined later in P02.

## 9. Ambiguity and rejection policy

The system must prefer explicit rejection over unjustified precision.

Examples such as:

```text
around 10
roughly above 10
a fairly high value
somewhere near 10
```

must not automatically become a precise operator unless the benchmark definition establishes that the intended meaning is objectively recoverable.

The proof distinguishes at least three outcomes:

```text
VALID_SUPPORTED_SEMANTICS
REJECT_AMBIGUOUS_OR_UNSUPPORTED
INVALID_MODEL_OUTPUT
```

The exact serialized representation of these outcomes is deferred to P01/P03.

A valid rejection policy must satisfy both sides:

- unsupported or genuinely ambiguous requests are not guessed;
- supported and unambiguous requests are not rejected merely to avoid semantic mistakes.

No confidence-gap threshold is frozen in P00. Calibration and uncertainty handling are experimental questions and may differ by model architecture.

## 10. English-first language plan

The first proof target is English.

English must pass its own frozen evaluation before broader language claims are made.

Bulgarian is a separate proof target later. A multilingual pretrained model does not, by itself, establish Bulgarian support.

Example distinctions that a later Bulgarian benchmark should test include:

```text
повече от 10       -> GT
поне 10            -> GTE
10 или повече      -> GTE
по-малко от 10     -> LT
най-много 10       -> LTE
не по-малко от 10  -> GTE
не повече от 10    -> LTE
```

Bulgarian requires its own training/development separation and its own sealed holdout.

The Semantic IR itself must remain language-independent.

## 11. Python research boundary

Vector remains a C#/.NET project.

Python is allowed only as isolated research/training tooling, expected under:

```text
research/
  npl/
```

During the proof phase:

- normal Vector execution must not require Python;
- `Vector.Core` must not depend on Python tooling;
- interpreter and VM behavior remain unchanged;
- research dependencies stay isolated from production/runtime dependencies.

If the semantic proof succeeds, deployment into normal Vector should prefer a .NET-friendly inference path, such as an exported model consumed from C#. Export/inference technology is intentionally not selected during P00.

## 12. Reproducibility requirements

Model experiments must be reviewable rather than anecdotal.

The research harness added later should preserve, where applicable:

- model identifier;
- model/configuration version;
- training/dev dataset version;
- random seed;
- relevant hyperparameters;
- checkpoint identity;
- evaluation-set identity;
- machine-readable predictions;
- exact-match results;
- invalid-output counts;
- rejection counts;
- confusion matrices;
- saved failure examples.

Long-running training remains a local user-run task; repository code should make those runs reproducible with simple commands.

## 13. Proof-stage sequence

The approved proof sequence is:

```text
NPL-P00  Proof architecture/documentation
NPL-P01  Semantic IR
NPL-P02  Frozen benchmark design and validation
NPL-P03  Research harness
NPL-P04  Training/dev datasets
NPL-P05  FLAN-T5 zero/few-shot baseline
NPL-P06  FLAN-T5 comparison fine-tuning
NPL-P07  Comparison hard gate
NPL-P08  Boolean composition/negation
NPL-P09  Arithmetic/value references
NPL-P10  Basic programming-intent semantics
NPL-P11  mT5 comparison
NPL-P12  Bulgarian proof
NPL-P13  Final semantic-recognition go/no-go report
```

Experiments are sequential. Results from one experiment determine the next experiment; later ML work should not be pre-built without seeing earlier evidence.

## 14. Final go/no-go rule

No Natural-language → Vector source generation begins before NPL-P13 passes.

A final **GO** requires evidence that the chosen recognition approach can:

- preserve critical comparison meanings without silent near-neighbour confusion;
- handle composition and negation at the required gate level;
- preserve identifiers and literals;
- produce structurally valid typed semantics;
- reject unsupported or ambiguous requests rather than guess;
- achieve the agreed broader exact-semantic accuracy on genuinely unseen human-written requests;
- reproduce its evaluation results sufficiently for review.

A **NO-GO** means code generation remains blocked.

Failure should trigger investigation of the recognition approach, including:

- training-data quality;
- hard-negative design and weighting;
- output representation;
- model size;
- classifier/encoder alternatives;
- contrastive or other objectives;
- calibration/rejection strategy;
- another pretrained model;
- only later, if justified by evidence, a custom model trained more substantially from scratch.

The acceptance criteria must not be weakened merely to allow downstream feature work to continue.

## 15. Explicit proof-phase non-goals

The following are outside P00-P13:

- Natural-language → Vector source generation;
- automatic execution;
- CLI execution from natural language;
- VS Code NPL commands;
- Visual Studio NPL commands;
- package-manager integration;
- a phrase/dictionary code generator;
- embeddings-as-nearest-operator selection;
- a general-purpose LLM trained from scratch;
- a giant natural-language grammar;
- broad multilingual claims;
- hidden fallback guessing.

If the proof succeeds, later generation/integration work begins as a separate gated phase.

## 16. NPL-P00 completion criteria

NPL-P00 is complete when:

- this architecture document is reviewed and accepted;
- the no-code-generation boundary is explicit;
- initial model candidates are recorded;
- initial semantic concepts are recorded without implementing P01;
- development data and sealed gate data are clearly separated;
- the hard comparison gate is explicit and cannot be passed by rejecting supported inputs;
- ambiguity/rejection behavior is explicit;
- English-first and Bulgarian-later evaluation are explicit;
- the research-only Python boundary is explicit;
- the final go/no-go rule is explicit;
- no runtime, Semantic IR, model, training, generation, or editor code has been added.

Only after explicit approval of NPL-P00 should NPL-P01 begin.
