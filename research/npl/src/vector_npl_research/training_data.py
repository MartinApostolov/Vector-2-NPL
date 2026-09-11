from __future__ import annotations

import json
import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

from .benchmark import BenchmarkCase, canonical_sha256, load_benchmark_jsonl
from .model_output import parse_model_output
from .semantic import (
    Comparison,
    ComparisonKind,
    Identifier,
    SemanticExpression,
    SemanticValidationError,
    load_json_strict,
    parse_semantic,
    semantic_to_data,
)

TRAINING_FILE = "comparison_training.jsonl"
DEVELOPMENT_FILE = "comparison_training_development.jsonl"
P02_VISIBLE_DEVELOPMENT_FILE = "comparison_development.jsonl"
EXPECTED_FILES = {
    TRAINING_FILE: ("training", "train"),
    DEVELOPMENT_FILE: ("training-development", "development"),
}
ALLOWED_RECORD_MEMBERS = {
    "id",
    "language",
    "split",
    "input",
    "expectation",
    "expected",
    "rejectReason",
    "targetOutput",
    "family",
    "tags",
    "pair",
}
CRITICAL_KIND_PAIRS = {
    frozenset((ComparisonKind.GT, ComparisonKind.GTE)),
    frozenset((ComparisonKind.LT, ComparisonKind.LTE)),
    frozenset((ComparisonKind.EQ, ComparisonKind.NEQ)),
}


class TrainingDataError(ValueError):
    pass


@dataclass(frozen=True, slots=True)
class TrainingRecord:
    case_id: str
    language: str
    split: str
    input_text: str
    expectation: str
    expected: Comparison | None
    reject_reason: str | None
    target_output: str
    family: str
    tags: tuple[str, ...]
    pair: str | None


@dataclass(frozen=True, slots=True)
class TrainingDataset:
    dataset: str
    dataset_file: str
    dataset_role: str
    dataset_sha256: str
    case_count: int
    split: str
    records: tuple[TrainingRecord, ...]


@dataclass(frozen=True, slots=True)
class DatasetLeak:
    code: str
    first_case_id: str
    second_case_id: str
    value: str


def load_training_jsonl(
    path: str | Path,
    *,
    expected_split: str,
) -> tuple[TrainingRecord, ...]:
    if expected_split not in {"train", "development"}:
        raise TrainingDataError(f"Unsupported expected split {expected_split!r}.")
    try:
        text = Path(path).read_bytes().decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise TrainingDataError("Training data must be valid UTF-8.") from error

    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    content = normalized[:-1] if normalized.endswith("\n") else normalized
    if not content:
        raise TrainingDataError("Training JSONL must contain at least one record.")

    records: list[TrainingRecord] = []
    ids: set[str] = set()
    inputs: dict[str, str] = {}
    for line_number, line in enumerate(content.split("\n"), start=1):
        if not line.strip():
            raise TrainingDataError(f"Training JSONL line {line_number} is empty.")
        try:
            value = load_json_strict(line)
        except SemanticValidationError as error:
            raise TrainingDataError(
                f"Invalid JSON on line {line_number}: {error.code} at {error.path}: {error.message}"
            ) from error
        record = _parse_training_record(value, line_number, expected_split)
        if record.case_id in ids:
            raise TrainingDataError(f"Duplicate training case id {record.case_id!r}.")
        ids.add(record.case_id)
        normalized_input = normalize_input(record.input_text)
        if normalized_input in inputs:
            raise TrainingDataError(
                f"Input for {record.case_id!r} duplicates {inputs[normalized_input]!r} after normalization."
            )
        inputs[normalized_input] = record.case_id
        records.append(record)

    result = tuple(records)
    validate_pair_integrity(result)
    return result


def prepare_training_dataset(
    dataset_path: str | Path,
    manifest_path: str | Path,
) -> TrainingDataset:
    dataset_path = Path(dataset_path)
    manifest = _load_dataset_manifest(Path(manifest_path))
    if dataset_path.name not in EXPECTED_FILES:
        raise TrainingDataError(f"Unexpected P04 dataset filename {dataset_path.name!r}.")
    role, split = EXPECTED_FILES[dataset_path.name]
    files = manifest["files"]
    assert isinstance(files, dict)
    entry = files.get(dataset_path.name)
    if not isinstance(entry, dict):
        raise TrainingDataError(f"Manifest does not declare {dataset_path.name!r}.")
    _require_exact_members(entry, {"role", "caseCount", "counts", "sha256"}, "manifest file")
    if entry["role"] != role:
        raise TrainingDataError(f"Manifest role for {dataset_path.name!r} must be {role!r}.")
    expected_hash = entry["sha256"]
    if not isinstance(expected_hash, str) or len(expected_hash) != 64:
        raise TrainingDataError("Manifest file hash must be a 64-character string.")
    actual_hash = canonical_sha256(dataset_path)
    if actual_hash != expected_hash:
        raise TrainingDataError(
            f"Canonical SHA-256 mismatch for {dataset_path.name}: "
            f"expected {expected_hash}, found {actual_hash}."
        )

    records = load_training_jsonl(dataset_path, expected_split=split)
    case_count = entry["caseCount"]
    if isinstance(case_count, bool) or not isinstance(case_count, int) or case_count < 0:
        raise TrainingDataError("Manifest caseCount must be a nonnegative integer.")
    if len(records) != case_count:
        raise TrainingDataError(
            f"Manifest caseCount is {case_count}, but {dataset_path.name} contains {len(records)} records."
        )
    actual_counts = count_dispositions(records)
    if entry["counts"] != actual_counts:
        raise TrainingDataError(
            f"Manifest class counts for {dataset_path.name!r} do not match the records."
        )

    dataset_name = manifest["dataset"]
    assert isinstance(dataset_name, str)
    return TrainingDataset(
        dataset=dataset_name,
        dataset_file=dataset_path.name,
        dataset_role=role,
        dataset_sha256=actual_hash,
        case_count=case_count,
        split=split,
        records=records,
    )


def verify_training_manifest(
    manifest_path: str | Path,
    dataset_directory: str | Path,
) -> tuple[TrainingDataset, TrainingDataset]:
    manifest_path = Path(manifest_path)
    directory = Path(dataset_directory)
    manifest = _load_dataset_manifest(manifest_path)
    _verify_manifest_contract(manifest)
    training = prepare_training_dataset(directory / TRAINING_FILE, manifest_path)
    development = prepare_training_dataset(directory / DEVELOPMENT_FILE, manifest_path)
    return training, development


def validate_pair_integrity(records: tuple[TrainingRecord, ...]) -> None:
    groups: dict[str, list[TrainingRecord]] = {}
    for record in records:
        if record.expectation != "semantic":
            continue
        if record.pair is None:
            raise TrainingDataError(f"Semantic record {record.case_id!r} requires a nonblank pair id.")
        groups.setdefault(record.pair, []).append(record)

    for pair_id, members in groups.items():
        if len(members) != 2:
            raise TrainingDataError(
                f"Semantic pair {pair_id!r} must contain exactly two records; found {len(members)}."
            )
        first, second = members
        assert first.expected is not None and second.expected is not None
        if frozenset((first.expected.kind, second.expected.kind)) not in CRITICAL_KIND_PAIRS:
            raise TrainingDataError(
                f"Semantic pair {pair_id!r} does not contain an approved near-neighbor kind pair."
            )
        if first.expected.left != second.expected.left:
            raise TrainingDataError(f"Semantic pair {pair_id!r} has differing left operands.")
        if first.expected.right != second.expected.right:
            raise TrainingDataError(f"Semantic pair {pair_id!r} has differing right operands.")


def count_pair_groups(records: tuple[TrainingRecord, ...]) -> int:
    return len({record.pair for record in records if record.expectation == "semantic"})


def count_dispositions(records: tuple[TrainingRecord, ...]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for record in records:
        label = record.expected.kind.value if record.expected is not None else "REJECT"
        counts[label] = counts.get(label, 0) + 1
    return {key: counts[key] for key in sorted(counts)}


def find_training_split_leaks(
    training: tuple[TrainingRecord, ...],
    development: tuple[TrainingRecord, ...],
) -> tuple[DatasetLeak, ...]:
    leaks: list[DatasetLeak] = []
    train_ids = {record.case_id: record for record in training}
    train_inputs = _first_by(training, lambda record: normalize_input(record.input_text))
    train_families = _first_by(training, lambda record: record.family)
    train_templates = _first_by(
        (record for record in training if record.expected is not None),
        create_semantic_template,
    )

    for record in development:
        if match := train_ids.get(record.case_id):
            leaks.append(DatasetLeak("P04_ID_OVERLAP", match.case_id, record.case_id, record.case_id))
        normalized_input = normalize_input(record.input_text)
        if match := train_inputs.get(normalized_input):
            leaks.append(
                DatasetLeak("P04_INPUT_OVERLAP", match.case_id, record.case_id, normalized_input)
            )
        if match := train_families.get(record.family):
            leaks.append(
                DatasetLeak("P04_FAMILY_OVERLAP", match.case_id, record.case_id, record.family)
            )
        if record.expected is not None:
            template = create_semantic_template(record)
            if match := train_templates.get(template):
                leaks.append(
                    DatasetLeak("P04_TEMPLATE_OVERLAP", match.case_id, record.case_id, template)
                )
    return tuple(leaks)


def validate_training_splits(
    training: tuple[TrainingRecord, ...],
    development: tuple[TrainingRecord, ...],
) -> None:
    leaks = find_training_split_leaks(training, development)
    if leaks:
        first = leaks[0]
        raise TrainingDataError(
            f"{first.code}: {first.first_case_id!r} overlaps {first.second_case_id!r}: {first.value}"
        )


def find_visible_development_leaks(
    p04_records: Iterable[TrainingRecord],
    visible_development: tuple[BenchmarkCase, ...],
) -> tuple[DatasetLeak, ...]:
    p04 = tuple(p04_records)
    p04_inputs = _first_by(p04, lambda record: normalize_input(record.input_text))
    p04_templates = _first_by(
        (record for record in p04 if record.expected is not None),
        create_semantic_template,
    )
    leaks: list[DatasetLeak] = []
    for case in visible_development:
        normalized_input = normalize_input(case.input_text)
        if match := p04_inputs.get(normalized_input):
            leaks.append(
                DatasetLeak("P02_VISIBLE_INPUT_OVERLAP", match.case_id, case.case_id, normalized_input)
            )
        if isinstance(case.expected, Comparison):
            template = create_template(case.input_text, case.expected)
            if match := p04_templates.get(template):
                leaks.append(
                    DatasetLeak("P02_VISIBLE_TEMPLATE_OVERLAP", match.case_id, case.case_id, template)
                )
    return tuple(leaks)


def validate_against_p02_visible_development(
    p04_records: Iterable[TrainingRecord],
    visible_development_path: str | Path,
) -> None:
    path = Path(visible_development_path)
    if path.name != P02_VISIBLE_DEVELOPMENT_FILE:
        raise TrainingDataError(
            "P04 leakage validation accepts only the visible P02 comparison development filename."
        )
    visible = load_benchmark_jsonl(path)
    leaks = find_visible_development_leaks(p04_records, visible)
    if leaks:
        first = leaks[0]
        raise TrainingDataError(
            f"{first.code}: {first.first_case_id!r} overlaps {first.second_case_id!r}: {first.value}"
        )


def normalize_input(text: str) -> str:
    normalized = unicodedata.normalize("NFKC", text).strip()
    return " ".join(normalized.split()).casefold()


def create_semantic_template(record: TrainingRecord) -> str:
    assert record.expected is not None
    return create_template(record.input_text, record.expected)


def create_template(input_text: str, expected: Comparison) -> str:
    template = unicodedata.normalize("NFKC", input_text)
    template = re.sub(r'"(?:[^"\\]|\\.)*"', "<TEXT>", template)
    template = re.sub(
        r"(?<![\w])[-+]?(?:\d+(?:\.\d+)?|\.\d+)(?![\w])",
        "<NUMBER>",
        template,
    )
    template = re.sub(r"\b(?:true|false)\b", "<BOOLEAN>", template, flags=re.IGNORECASE)
    for identifier in sorted(_identifiers(expected), key=len, reverse=True):
        template = re.sub(
            rf"(?<![\w]){re.escape(identifier)}(?![\w])",
            "<ID>",
            template,
            flags=re.IGNORECASE,
        )
    return normalize_input(template)


def canonical_target_output(expected: Comparison | None) -> str:
    value: dict[str, Any]
    if expected is None:
        value = {"outcome": "reject"}
    else:
        value = {"outcome": "semantic", "semantic": semantic_to_data(expected)}
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def _parse_training_record(value: Any, line_number: int, expected_split: str) -> TrainingRecord:
    if not isinstance(value, dict):
        raise TrainingDataError(f"Training JSONL line {line_number} must be a JSON object.")
    unknown = sorted(set(value) - ALLOWED_RECORD_MEMBERS)
    if unknown:
        raise TrainingDataError(
            f"Unknown training record member {unknown[0]!r} on line {line_number}."
        )

    case_id = _required_nonblank_string(value, "id", line_number)
    language = _required_nonblank_string(value, "language", line_number)
    if language != "en":
        raise TrainingDataError(f"Training language must be exactly 'en' on line {line_number}.")
    split = _required_nonblank_string(value, "split", line_number)
    if split not in {"train", "development"}:
        raise TrainingDataError(f"Invalid training split {split!r} on line {line_number}.")
    if split != expected_split:
        raise TrainingDataError(
            f"Record split {split!r} does not match expected split {expected_split!r} "
            f"on line {line_number}."
        )
    input_text = _required_nonblank_string(value, "input", line_number)
    expectation = _required_nonblank_string(value, "expectation", line_number)
    target_output = _required_nonblank_string(value, "targetOutput", line_number)
    family = _required_nonblank_string(value, "family", line_number)
    tags = _required_tags(value, line_number)
    pair = _optional_nonblank_string(value, "pair", line_number)
    expected: Comparison | None = None
    reject_reason: str | None = None

    if expectation == "semantic":
        if "expected" not in value:
            raise TrainingDataError(f"Semantic record lacks expected IR on line {line_number}.")
        if "rejectReason" in value:
            raise TrainingDataError(
                f"Semantic record contains rejectReason on line {line_number}."
            )
        if pair is None:
            raise TrainingDataError(f"Semantic record lacks a nonblank pair on line {line_number}.")
        try:
            parsed_expected = parse_semantic(value["expected"], f"$[{line_number}].expected")
        except SemanticValidationError as error:
            raise TrainingDataError(
                f"Invalid expected IR on line {line_number}: "
                f"{error.code} at {error.path}: {error.message}"
            ) from error
        if not isinstance(parsed_expected, Comparison):
            raise TrainingDataError(
                f"Semantic expected IR must have a comparison root on line {line_number}."
            )
        expected = parsed_expected
    elif expectation == "reject":
        if "expected" in value:
            raise TrainingDataError(f"Reject record contains expected IR on line {line_number}.")
        reject_reason = _required_nonblank_string(value, "rejectReason", line_number)
    else:
        raise TrainingDataError(
            f"Expectation must be exactly 'semantic' or 'reject' on line {line_number}."
        )

    parsed_target = parse_model_output(target_output)
    if not parsed_target.valid:
        raise TrainingDataError(
            f"Invalid targetOutput on line {line_number}: "
            f"{parsed_target.error_code} at {parsed_target.error_path}: "
            f"{parsed_target.error_message}"
        )
    if expected is not None:
        if parsed_target.outcome != "semantic":
            raise TrainingDataError(f"Semantic record targets rejection on line {line_number}.")
        if parsed_target.semantic != expected:
            raise TrainingDataError(
                f"Semantic targetOutput differs from expected IR on line {line_number}."
            )
    elif parsed_target.outcome != "reject":
        raise TrainingDataError(f"Reject record targets semantics on line {line_number}.")

    canonical = canonical_target_output(expected)
    if target_output != canonical:
        raise TrainingDataError(f"targetOutput is not canonical on line {line_number}.")

    return TrainingRecord(
        case_id=case_id,
        language=language,
        split=split,
        input_text=input_text,
        expectation=expectation,
        expected=expected,
        reject_reason=reject_reason,
        target_output=target_output,
        family=family,
        tags=tags,
        pair=pair,
    )


def _load_dataset_manifest(path: Path) -> dict[str, Any]:
    try:
        value = load_json_strict(path.read_text(encoding="utf-8-sig"))
    except (OSError, SemanticValidationError) as error:
        raise TrainingDataError(f"Invalid P04 dataset manifest: {error}") from error
    if not isinstance(value, dict):
        raise TrainingDataError("P04 dataset manifest must be a JSON object.")
    _require_exact_members(
        value,
        {
            "schemaVersion",
            "dataset",
            "language",
            "modelOutputContract",
            "files",
            "splitPolicy",
            "generation",
            "hashContract",
        },
        "dataset manifest",
    )
    if value["schemaVersion"] != 1:
        raise TrainingDataError("Unsupported P04 dataset manifest schemaVersion.")
    if value["dataset"] != "vector-npl-comparison-training-v1":
        raise TrainingDataError("Unexpected P04 dataset identifier.")
    if value["language"] != "en":
        raise TrainingDataError("P04 dataset language must be 'en'.")
    if value["modelOutputContract"] != "p03-v1":
        raise TrainingDataError("P04 modelOutputContract must be 'p03-v1'.")
    if not isinstance(value["files"], dict) or set(value["files"]) != set(EXPECTED_FILES):
        raise TrainingDataError("P04 manifest must declare exactly the two frozen dataset files.")
    return value


def _verify_manifest_contract(manifest: dict[str, Any]) -> None:
    split_policy = manifest["splitPolicy"]
    generation = manifest["generation"]
    hash_contract = manifest["hashContract"]
    if not isinstance(split_policy, dict):
        raise TrainingDataError("Manifest splitPolicy must be an object.")
    _require_exact_members(
        split_policy,
        {
            "phraseFamiliesDisjoint",
            "exactInputsDisjoint",
            "identifiersAndLiteralPoolsIntentionallyMostlyDisjoint",
            "p02DevelopmentExactInputOverlap",
            "p02DevelopmentSemanticTemplateOverlap",
            "p02SealedGateConsultedForDatasetConstruction",
        },
        "splitPolicy",
    )
    expected_policy = {
        "phraseFamiliesDisjoint": True,
        "exactInputsDisjoint": True,
        "identifiersAndLiteralPoolsIntentionallyMostlyDisjoint": True,
        "p02DevelopmentExactInputOverlap": 0,
        "p02DevelopmentSemanticTemplateOverlap": 0,
        "p02SealedGateConsultedForDatasetConstruction": False,
    }
    if split_policy != expected_policy:
        raise TrainingDataError("Manifest splitPolicy does not match the frozen P04 policy.")

    if not isinstance(generation, dict):
        raise TrainingDataError("Manifest generation must be an object.")
    _require_exact_members(generation, {"method", "randomSeed", "targetOutput"}, "generation")
    if generation != {
        "method": "deterministic curated phrase-family expansion",
        "randomSeed": None,
        "targetOutput": "strict P03 model-output envelope",
    }:
        raise TrainingDataError("Manifest generation metadata does not match the frozen contract.")

    if not isinstance(hash_contract, dict):
        raise TrainingDataError("Manifest hashContract must be an object.")
    _require_exact_members(
        hash_contract,
        {"algorithm", "encoding", "lineEndings", "terminalNewline"},
        "hashContract",
    )
    if hash_contract != {
        "algorithm": "SHA-256",
        "encoding": "UTF-8 without BOM",
        "lineEndings": "Normalize CRLF and CR to LF",
        "terminalNewline": "Exactly one LF",
    }:
        raise TrainingDataError("Manifest hashContract does not match the canonical hash contract.")


def _required_nonblank_string(value: dict[str, Any], name: str, line_number: int) -> str:
    item = value.get(name)
    if not isinstance(item, str) or not item.strip():
        raise TrainingDataError(
            f"Member {name!r} must be a nonblank string on line {line_number}."
        )
    return item


def _optional_nonblank_string(
    value: dict[str, Any], name: str, line_number: int
) -> str | None:
    if name not in value:
        return None
    item = value[name]
    if not isinstance(item, str) or not item.strip():
        raise TrainingDataError(
            f"Optional member {name!r} must be a nonblank string on line {line_number}."
        )
    return item


def _required_tags(value: dict[str, Any], line_number: int) -> tuple[str, ...]:
    tags = value.get("tags")
    if not isinstance(tags, list) or not tags:
        raise TrainingDataError(f"Tags must be a nonempty array on line {line_number}.")
    if any(not isinstance(tag, str) or not tag.strip() for tag in tags):
        raise TrainingDataError(f"Every tag must be a nonblank string on line {line_number}.")
    return tuple(tags)


def _require_exact_members(value: dict[str, Any], expected: set[str], description: str) -> None:
    actual = set(value)
    missing = sorted(expected - actual)
    unknown = sorted(actual - expected)
    if missing:
        raise TrainingDataError(f"{description} is missing member {missing[0]!r}.")
    if unknown:
        raise TrainingDataError(f"{description} contains unknown member {unknown[0]!r}.")


def _identifiers(expression: SemanticExpression) -> tuple[str, ...]:
    if isinstance(expression, Identifier):
        return (expression.name,)
    if isinstance(expression, Comparison):
        return _identifiers(expression.left) + _identifiers(expression.right)
    return ()


def _first_by(
    records: Iterable[TrainingRecord],
    key_selector: Any,
) -> dict[str, TrainingRecord]:
    result: dict[str, TrainingRecord] = {}
    for record in records:
        result.setdefault(key_selector(record), record)
    return result
