from __future__ import annotations

import json
from typing import Any

from vector_npl_research.semantic import Comparison, ComparisonKind, Identifier, NumberLiteral
from vector_npl_research.training_data import TrainingRecord, canonical_target_output


def expected_data(
    kind: str = "GT",
    identifier: str = "score",
    number: int = 10,
) -> dict[str, Any]:
    return {
        "type": "comparison",
        "kind": kind,
        "left": {"type": "identifier", "name": identifier},
        "right": {"type": "numberLiteral", "value": number},
    }


def semantic_data(
    *,
    case_id: str = "case-gt",
    split: str = "train",
    kind: str = "GT",
    identifier: str = "score",
    number: int = 10,
    pair: str | None = "pair-1",
) -> dict[str, Any]:
    expected = expected_data(kind, identifier, number)
    value: dict[str, Any] = {
        "id": case_id,
        "language": "en",
        "split": split,
        "input": f"{identifier} is compared with {number}.",
        "expectation": "semantic",
        "expected": expected,
        "targetOutput": json.dumps(
            {"outcome": "semantic", "semantic": expected},
            separators=(",", ":"),
        ),
        "family": f"family-{case_id}",
        "tags": [kind, "test"],
    }
    if pair is not None:
        value["pair"] = pair
    return value


def reject_data(
    *,
    case_id: str = "case-reject",
    split: str = "train",
) -> dict[str, Any]:
    return {
        "id": case_id,
        "language": "en",
        "split": split,
        "input": f"ambiguous input for {case_id}",
        "expectation": "reject",
        "rejectReason": "ambiguous",
        "targetOutput": '{"outcome":"reject"}',
        "family": f"family-{case_id}",
        "tags": ["REJECT", "test"],
    }


def jsonl(*values: dict[str, Any]) -> str:
    return "\n".join(json.dumps(value, separators=(",", ":")) for value in values) + "\n"


def semantic_record(
    case_id: str,
    kind: ComparisonKind,
    pair: str | None,
    *,
    identifier: str = "score",
    number: int = 10,
    family: str | None = None,
    input_text: str | None = None,
    split: str = "train",
) -> TrainingRecord:
    expected = Comparison(kind, Identifier(identifier), NumberLiteral(number))
    return TrainingRecord(
        case_id=case_id,
        language="en",
        split=split,
        input_text=input_text or f"{identifier} template {number} {case_id}",
        expectation="semantic",
        expected=expected,
        reject_reason=None,
        target_output=canonical_target_output(expected),
        family=family or f"family-{case_id}",
        tags=(kind.value,),
        pair=pair,
    )


def reject_record(
    case_id: str,
    *,
    family: str | None = None,
    input_text: str | None = None,
    split: str = "train",
) -> TrainingRecord:
    return TrainingRecord(
        case_id=case_id,
        language="en",
        split=split,
        input_text=input_text or f"ambiguous {case_id}",
        expectation="reject",
        expected=None,
        reject_reason="ambiguous",
        target_output='{"outcome":"reject"}',
        family=family or f"family-{case_id}",
        tags=("REJECT",),
        pair=None,
    )
