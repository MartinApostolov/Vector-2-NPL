from __future__ import annotations

import hashlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .semantic import Comparison, SemanticExpression, SemanticValidationError, load_json_strict, parse_semantic


class BenchmarkError(ValueError):
    pass


class SealedGateBlockedError(BenchmarkError):
    pass


@dataclass(frozen=True, slots=True)
class BenchmarkCase:
    case_id: str
    language: str
    input_text: str
    expectation: str
    expected: SemanticExpression | None
    reject_reason: str | None
    tags: tuple[str, ...]
    pair: str | None
    notes: str | None


@dataclass(frozen=True, slots=True)
class DatasetMetadata:
    benchmark: str
    dataset_file: str
    dataset_role: str
    dataset_sha256: str
    case_count: int


@dataclass(frozen=True, slots=True)
class BenchmarkSelection:
    metadata: DatasetMetadata
    cases: tuple[BenchmarkCase, ...]


def canonicalize_benchmark_bytes(data: bytes) -> bytes:
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise BenchmarkError("Benchmark must be valid UTF-8.") from error
    canonical = text.replace("\r\n", "\n").replace("\r", "\n").rstrip("\n") + "\n"
    return canonical.encode("utf-8")


def canonical_sha256(path: str | Path) -> str:
    data = Path(path).read_bytes()
    return hashlib.sha256(canonicalize_benchmark_bytes(data)).hexdigest()


def prepare_benchmark(
    dataset_path: str | Path,
    manifest_path: str | Path,
    *,
    allow_sealed_gate: bool = False,
) -> BenchmarkSelection:
    dataset = Path(dataset_path)
    manifest = _load_manifest(Path(manifest_path))
    files = manifest["files"]
    assert isinstance(files, dict)
    file_entry = files.get(dataset.name)
    if not isinstance(file_entry, dict):
        raise BenchmarkError(f"Dataset {dataset.name!r} is not declared by the manifest.")

    _require_exact_members(file_entry, {"role", "caseCount", "counts", "sha256"}, "manifest file")
    role = file_entry["role"]
    case_count = file_entry["caseCount"]
    expected_hash = file_entry["sha256"]
    if role not in {"development", "sealed-gate"}:
        raise BenchmarkError(f"Unsupported dataset role: {role!r}.")
    if isinstance(case_count, bool) or not isinstance(case_count, int) or case_count < 0:
        raise BenchmarkError("Manifest caseCount must be a nonnegative integer.")
    if not isinstance(expected_hash, str) or len(expected_hash) != 64:
        raise BenchmarkError("Manifest sha256 must be a 64-character string.")

    actual_hash = canonical_sha256(dataset)
    if actual_hash != expected_hash:
        raise BenchmarkError(
            f"Canonical SHA-256 mismatch for {dataset.name}: expected {expected_hash}, found {actual_hash}."
        )

    if role == "sealed-gate" and not allow_sealed_gate:
        raise SealedGateBlockedError(
            "The selected dataset has role 'sealed-gate' and is blocked by default. "
            "The explicit override is reserved for the future P07 gate."
        )

    cases = load_benchmark_jsonl(dataset)
    if len(cases) != case_count:
        raise BenchmarkError(
            f"Manifest caseCount is {case_count}, but {dataset.name} contains {len(cases)} cases."
        )

    benchmark_name = manifest["benchmark"]
    assert isinstance(benchmark_name, str)
    return BenchmarkSelection(
        metadata=DatasetMetadata(
            benchmark=benchmark_name,
            dataset_file=dataset.name,
            dataset_role=role,
            dataset_sha256=actual_hash,
            case_count=case_count,
        ),
        cases=cases,
    )


def load_benchmark_jsonl(path: str | Path) -> tuple[BenchmarkCase, ...]:
    try:
        text = Path(path).read_bytes().decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise BenchmarkError("Benchmark must be valid UTF-8.") from error
    lines = text.replace("\r\n", "\n").replace("\r", "\n").rstrip("\n").split("\n")
    if lines == [""]:
        raise BenchmarkError("Benchmark must contain at least one case.")

    cases: list[BenchmarkCase] = []
    ids: set[str] = set()
    for line_number, line in enumerate(lines, start=1):
        if not line.strip():
            raise BenchmarkError(f"Benchmark line {line_number} is empty.")
        try:
            value = load_json_strict(line)
            benchmark_case = _parse_case(value, line_number)
        except SemanticValidationError as error:
            raise BenchmarkError(
                f"Invalid benchmark case on line {line_number}: {error.code} at {error.path}: {error.message}"
            ) from error
        if benchmark_case.case_id in ids:
            raise BenchmarkError(f"Duplicate benchmark case id: {benchmark_case.case_id!r}.")
        ids.add(benchmark_case.case_id)
        cases.append(benchmark_case)
    return tuple(cases)


def _load_manifest(path: Path) -> dict[str, Any]:
    try:
        value = load_json_strict(path.read_text(encoding="utf-8-sig"))
    except (OSError, SemanticValidationError) as error:
        raise BenchmarkError(f"Invalid benchmark manifest: {error}") from error
    if not isinstance(value, dict):
        raise BenchmarkError("Benchmark manifest must be a JSON object.")
    _require_exact_members(
        value,
        {"schemaVersion", "benchmark", "language", "files", "policy", "hashContract"},
        "manifest",
    )
    if value["schemaVersion"] != 1:
        raise BenchmarkError("Unsupported benchmark manifest schemaVersion.")
    if not isinstance(value["benchmark"], str) or not value["benchmark"].strip():
        raise BenchmarkError("Manifest benchmark must be a nonblank string.")
    if value["language"] != "en":
        raise BenchmarkError("Comparison manifest language must be 'en'.")
    if not isinstance(value["files"], dict):
        raise BenchmarkError("Manifest files must be an object.")
    if not isinstance(value["policy"], dict) or not isinstance(value["hashContract"], dict):
        raise BenchmarkError("Manifest policy and hashContract must be objects.")
    return value


def _parse_case(value: Any, line_number: int) -> BenchmarkCase:
    if not isinstance(value, dict):
        raise BenchmarkError(f"Benchmark line {line_number} must be an object.")
    allowed = {
        "id",
        "language",
        "input",
        "expectation",
        "expected",
        "rejectReason",
        "tags",
        "pair",
        "notes",
    }
    unknown = sorted(set(value) - allowed)
    if unknown:
        raise BenchmarkError(f"Unknown benchmark member {unknown[0]!r} on line {line_number}.")

    case_id = _nonblank_string(value, "id", line_number)
    language = _nonblank_string(value, "language", line_number)
    if language != "en":
        raise BenchmarkError(f"Unsupported benchmark language on line {line_number}.")
    input_text = _nonblank_string(value, "input", line_number)
    expectation = _nonblank_string(value, "expectation", line_number)
    tags_value = value.get("tags")
    if (
        not isinstance(tags_value, list)
        or not tags_value
        or any(not isinstance(tag, str) or not tag.strip() for tag in tags_value)
    ):
        raise BenchmarkError(f"Benchmark tags must be a nonempty array of nonblank strings on line {line_number}.")

    pair = _optional_string(value, "pair", line_number)
    notes = _optional_string(value, "notes", line_number)
    expected: SemanticExpression | None = None
    reject_reason: str | None = None

    if expectation == "semantic":
        if "expected" not in value:
            raise BenchmarkError(f"Semantic case lacks expected IR on line {line_number}.")
        if "rejectReason" in value:
            raise BenchmarkError(f"Semantic case contains rejectReason on line {line_number}.")
        expected = parse_semantic(value["expected"], f"$[{line_number}].expected")
        if not isinstance(expected, Comparison):
            raise BenchmarkError(f"Semantic case expected root must be comparison on line {line_number}.")
    elif expectation == "reject":
        if "expected" in value:
            raise BenchmarkError(f"Rejection case contains expected IR on line {line_number}.")
        reject_reason = _nonblank_string(value, "rejectReason", line_number)
    else:
        raise BenchmarkError(f"Unsupported benchmark expectation on line {line_number}.")

    return BenchmarkCase(
        case_id=case_id,
        language=language,
        input_text=input_text,
        expectation=expectation,
        expected=expected,
        reject_reason=reject_reason,
        tags=tuple(tags_value),
        pair=pair,
        notes=notes,
    )


def _nonblank_string(value: dict[str, Any], name: str, line_number: int) -> str:
    item = value.get(name)
    if not isinstance(item, str) or not item.strip():
        raise BenchmarkError(f"Member {name!r} must be a nonblank string on line {line_number}.")
    return item


def _optional_string(value: dict[str, Any], name: str, line_number: int) -> str | None:
    if name not in value:
        return None
    item = value[name]
    if not isinstance(item, str):
        raise BenchmarkError(f"Optional member {name!r} must be a string on line {line_number}.")
    return item


def _require_exact_members(value: dict[str, Any], expected: set[str], description: str) -> None:
    actual = set(value)
    missing = sorted(expected - actual)
    unknown = sorted(actual - expected)
    if missing:
        raise BenchmarkError(f"{description} is missing member {missing[0]!r}.")
    if unknown:
        raise BenchmarkError(f"{description} contains unknown member {unknown[0]!r}.")
