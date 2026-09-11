from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .semantic import SemanticValidationError, load_json_strict


class PredictionError(ValueError):
    pass


@dataclass(frozen=True, slots=True)
class Prediction:
    case_id: str
    raw_output: str


def load_prediction_file(
    path: str | Path,
    benchmark_case_ids: set[str] | frozenset[str],
    *,
    require_complete: bool = True,
) -> tuple[Prediction, ...]:
    try:
        text = Path(path).read_bytes().decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise PredictionError("Prediction file must be valid UTF-8.") from error
    return load_prediction_jsonl(text, benchmark_case_ids, require_complete=require_complete)


def load_prediction_jsonl(
    text: str,
    benchmark_case_ids: set[str] | frozenset[str],
    *,
    require_complete: bool = True,
) -> tuple[Prediction, ...]:
    lines = text.replace("\r\n", "\n").replace("\r", "\n").rstrip("\n").split("\n")
    if lines == [""]:
        raise PredictionError("Prediction JSONL must contain at least one record.")

    predictions: dict[str, Prediction] = {}
    for line_number, line in enumerate(lines, start=1):
        if not line.strip():
            raise PredictionError(f"Prediction line {line_number} is empty.")
        try:
            value = load_json_strict(line)
        except SemanticValidationError as error:
            raise PredictionError(
                f"Invalid prediction JSON on line {line_number}: {error.code}: {error.message}"
            ) from error
        prediction = _parse_prediction(value, line_number)
        if prediction.case_id in predictions:
            raise PredictionError(f"Duplicate prediction caseId {prediction.case_id!r}.")
        if prediction.case_id not in benchmark_case_ids:
            raise PredictionError(f"Unknown prediction caseId {prediction.case_id!r}.")
        predictions[prediction.case_id] = prediction

    if require_complete:
        missing = sorted(benchmark_case_ids - predictions.keys())
        if missing:
            raise PredictionError(f"Missing predictions for case IDs: {', '.join(missing)}.")

    return tuple(predictions[case_id] for case_id in sorted(predictions))


def _parse_prediction(value: Any, line_number: int) -> Prediction:
    if not isinstance(value, dict):
        raise PredictionError(f"Prediction line {line_number} must be a JSON object.")
    expected_members = {"caseId", "rawOutput"}
    missing = sorted(expected_members - set(value))
    unknown = sorted(set(value) - expected_members)
    if missing:
        raise PredictionError(f"Prediction line {line_number} lacks member {missing[0]!r}.")
    if unknown:
        raise PredictionError(f"Prediction line {line_number} has unknown member {unknown[0]!r}.")
    case_id = value["caseId"]
    raw_output = value["rawOutput"]
    if not isinstance(case_id, str) or not case_id.strip():
        raise PredictionError(f"Prediction caseId must be a nonblank string on line {line_number}.")
    if not isinstance(raw_output, str):
        raise PredictionError(f"Prediction rawOutput must be a string on line {line_number}.")
    return Prediction(case_id=case_id, raw_output=raw_output)
