from __future__ import annotations

import json
import math
from dataclasses import dataclass
from enum import Enum
from typing import Any


class SemanticValidationError(ValueError):
    def __init__(self, code: str, path: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.path = path
        self.message = message


class ComparisonKind(str, Enum):
    GT = "GT"
    GTE = "GTE"
    LT = "LT"
    LTE = "LTE"
    EQ = "EQ"
    NEQ = "NEQ"


@dataclass(frozen=True, slots=True)
class SemanticExpression:
    pass


@dataclass(frozen=True, slots=True)
class Comparison(SemanticExpression):
    kind: ComparisonKind
    left: SemanticExpression
    right: SemanticExpression


@dataclass(frozen=True, slots=True)
class Identifier(SemanticExpression):
    name: str


@dataclass(frozen=True, slots=True)
class NumberLiteral(SemanticExpression):
    value: int | float


@dataclass(frozen=True, slots=True)
class TextLiteral(SemanticExpression):
    value: str


@dataclass(frozen=True, slots=True)
class BooleanLiteral(SemanticExpression):
    value: bool


@dataclass(frozen=True, slots=True)
class CurrentItem(SemanticExpression):
    pass


def _object_without_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for name, value in pairs:
        if name in result:
            raise SemanticValidationError(
                "JSON_MEMBER_DUPLICATE",
                f"$.{name}",
                f"JSON member {name!r} appears more than once.",
            )
        result[name] = value
    return result


def _reject_nonfinite_constant(value: str) -> None:
    raise SemanticValidationError(
        "JSON_NUMBER_NOT_FINITE",
        "$",
        f"Non-finite JSON number {value!r} is not allowed.",
    )


def load_json_strict(text: str) -> Any:
    if not isinstance(text, str):
        raise SemanticValidationError("JSON_TEXT_REQUIRED", "$", "JSON input must be a string.")
    try:
        return json.loads(
            text,
            object_pairs_hook=_object_without_duplicates,
            parse_constant=_reject_nonfinite_constant,
        )
    except SemanticValidationError:
        raise
    except (json.JSONDecodeError, RecursionError) as error:
        raise SemanticValidationError("JSON_MALFORMED", "$", str(error)) from error


def parse_semantic_json(text: str) -> SemanticExpression:
    return parse_semantic(load_json_strict(text))


def parse_semantic(value: Any, path: str = "$") -> SemanticExpression:
    if not isinstance(value, dict):
        raise SemanticValidationError(
            "SEMANTIC_OBJECT_REQUIRED", path, "A semantic node must be a JSON object."
        )

    node_type = value.get("type")
    if not isinstance(node_type, str):
        raise SemanticValidationError(
            "SEMANTIC_TYPE_REQUIRED", f"{path}.type", "Semantic node type must be a string."
        )

    if node_type == "comparison":
        _require_exact_members(value, {"type", "kind", "left", "right"}, path)
        kind_value = value["kind"]
        if not isinstance(kind_value, str):
            raise SemanticValidationError(
                "COMPARISON_KIND_INVALID",
                f"{path}.kind",
                "Comparison kind must be a semantic name string.",
            )
        try:
            kind = ComparisonKind(kind_value)
        except ValueError as error:
            raise SemanticValidationError(
                "COMPARISON_KIND_INVALID",
                f"{path}.kind",
                f"Unsupported comparison kind {kind_value!r}.",
            ) from error
        return Comparison(
            kind=kind,
            left=parse_semantic(value["left"], f"{path}.left"),
            right=parse_semantic(value["right"], f"{path}.right"),
        )

    if node_type == "identifier":
        _require_exact_members(value, {"type", "name"}, path)
        name = value["name"]
        if not isinstance(name, str) or not name.strip():
            raise SemanticValidationError(
                "IDENTIFIER_NAME_REQUIRED",
                f"{path}.name",
                "Identifier name must be a nonblank string.",
            )
        return Identifier(name)

    if node_type == "numberLiteral":
        _require_exact_members(value, {"type", "value"}, path)
        number = value["value"]
        if isinstance(number, bool) or not isinstance(number, (int, float)):
            raise SemanticValidationError(
                "NUMBER_VALUE_INVALID",
                f"{path}.value",
                "Number literal value must be a JSON number and not a boolean.",
            )
        try:
            finite = math.isfinite(number)
        except OverflowError as error:
            raise SemanticValidationError(
                "NUMBER_VALUE_INVALID", f"{path}.value", "Number literal is outside finite range."
            ) from error
        if not finite:
            raise SemanticValidationError(
                "NUMBER_NOT_FINITE", f"{path}.value", "Number literal must be finite."
            )
        return NumberLiteral(number)

    if node_type == "textLiteral":
        _require_exact_members(value, {"type", "value"}, path)
        text = value["value"]
        if not isinstance(text, str):
            raise SemanticValidationError(
                "TEXT_VALUE_REQUIRED", f"{path}.value", "Text literal value must be a string."
            )
        return TextLiteral(text)

    if node_type == "booleanLiteral":
        _require_exact_members(value, {"type", "value"}, path)
        boolean = value["value"]
        if not isinstance(boolean, bool):
            raise SemanticValidationError(
                "BOOLEAN_VALUE_INVALID",
                f"{path}.value",
                "Boolean literal value must be a JSON boolean.",
            )
        return BooleanLiteral(boolean)

    if node_type == "currentItem":
        _require_exact_members(value, {"type"}, path)
        return CurrentItem()

    raise SemanticValidationError(
        "SEMANTIC_TYPE_UNKNOWN", f"{path}.type", f"Unsupported semantic node type {node_type!r}."
    )


def semantic_to_data(expression: SemanticExpression) -> dict[str, Any]:
    if isinstance(expression, Comparison):
        return {
            "type": "comparison",
            "kind": expression.kind.value,
            "left": semantic_to_data(expression.left),
            "right": semantic_to_data(expression.right),
        }
    if isinstance(expression, Identifier):
        return {"type": "identifier", "name": expression.name}
    if isinstance(expression, NumberLiteral):
        return {"type": "numberLiteral", "value": expression.value}
    if isinstance(expression, TextLiteral):
        return {"type": "textLiteral", "value": expression.value}
    if isinstance(expression, BooleanLiteral):
        return {"type": "booleanLiteral", "value": expression.value}
    if isinstance(expression, CurrentItem):
        return {"type": "currentItem"}
    raise TypeError(f"Unsupported semantic expression: {type(expression).__name__}")


def _require_exact_members(value: dict[str, Any], required: set[str], path: str) -> None:
    actual = set(value)
    missing = sorted(required - actual)
    if missing:
        name = missing[0]
        raise SemanticValidationError(
            "SEMANTIC_MEMBER_REQUIRED", f"{path}.{name}", f"Required member {name!r} is missing."
        )
    unknown = sorted(actual - required)
    if unknown:
        name = unknown[0]
        raise SemanticValidationError(
            "SEMANTIC_MEMBER_UNKNOWN", f"{path}.{name}", f"Member {name!r} is not allowed."
        )
