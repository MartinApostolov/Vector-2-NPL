from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

from .semantic import (
    Comparison,
    SemanticExpression,
    SemanticValidationError,
    load_json_strict,
    parse_semantic,
)


@dataclass(frozen=True, slots=True)
class ModelOutput:
    outcome: Literal["semantic", "reject", "invalid"]
    semantic: SemanticExpression | None = None
    error_code: str | None = None
    error_path: str | None = None
    error_message: str | None = None

    @property
    def valid(self) -> bool:
        return self.outcome != "invalid"


def parse_model_output(raw_output: str) -> ModelOutput:
    try:
        value = load_json_strict(raw_output)
        return _parse_envelope(value)
    except SemanticValidationError as error:
        return ModelOutput(
            outcome="invalid",
            error_code=error.code,
            error_path=error.path,
            error_message=error.message,
        )


def _parse_envelope(value: object) -> ModelOutput:
    if not isinstance(value, dict):
        raise SemanticValidationError(
            "OUTPUT_ENVELOPE_OBJECT_REQUIRED", "$", "Model output must be a JSON object."
        )

    outcome = value.get("outcome")
    if not isinstance(outcome, str):
        raise SemanticValidationError(
            "OUTPUT_OUTCOME_REQUIRED", "$.outcome", "Output outcome must be a string."
        )

    if outcome == "semantic":
        _require_members(value, {"outcome", "semantic"})
        semantic = parse_semantic(value["semantic"], "$.semantic")
        if not isinstance(semantic, Comparison):
            raise SemanticValidationError(
                "OUTPUT_COMPARISON_REQUIRED",
                "$.semantic",
                "Comparison-proof semantic output must have a comparison root.",
            )
        return ModelOutput(outcome="semantic", semantic=semantic)

    if outcome == "reject":
        _require_members(value, {"outcome"})
        return ModelOutput(outcome="reject")

    raise SemanticValidationError(
        "OUTPUT_OUTCOME_UNSUPPORTED",
        "$.outcome",
        "Output outcome must be exactly 'semantic' or 'reject'.",
    )


def _require_members(value: dict[str, object], expected: set[str]) -> None:
    actual = set(value)
    missing = sorted(expected - actual)
    if missing:
        name = missing[0]
        raise SemanticValidationError(
            "OUTPUT_MEMBER_REQUIRED", f"$.{name}", f"Required output member {name!r} is missing."
        )
    unknown = sorted(actual - expected)
    if unknown:
        name = unknown[0]
        raise SemanticValidationError(
            "OUTPUT_MEMBER_UNKNOWN", f"$.{name}", f"Output member {name!r} is not allowed."
        )
