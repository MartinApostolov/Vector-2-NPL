from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from .benchmark import BenchmarkCase
from .model_output import ModelOutput, parse_model_output
from .predictions import Prediction
from .semantic import Comparison, semantic_to_data

LABELS = ("GT", "GTE", "LT", "LTE", "EQ", "NEQ", "REJECT", "INVALID")
CRITICAL_PAIRS = {
    frozenset(("GT", "GTE")): "GT<->GTE",
    frozenset(("LT", "LTE")): "LT<->LTE",
    frozenset(("EQ", "NEQ")): "EQ<->NEQ",
}


@dataclass(frozen=True, slots=True)
class CaseResult:
    case_id: str
    expected_label: str
    actual_label: str
    passed: bool
    failure_category: str | None
    critical_confusion: str | None
    semantic_differences: tuple[str, ...]
    expected_semantic: dict[str, Any] | None
    actual_semantic: dict[str, Any] | None
    raw_output: str

    def to_data(self) -> dict[str, Any]:
        return {
            "caseId": self.case_id,
            "expectedLabel": self.expected_label,
            "actualLabel": self.actual_label,
            "passed": self.passed,
            "failureCategory": self.failure_category,
            "criticalConfusion": self.critical_confusion,
            "semanticDifferences": list(self.semantic_differences),
            "expectedSemantic": self.expected_semantic,
            "actualSemantic": self.actual_semantic,
            "rawOutput": self.raw_output,
        }


@dataclass(frozen=True, slots=True)
class ScoreReport:
    results: tuple[CaseResult, ...]
    summary: dict[str, Any]


def score_predictions(
    cases: tuple[BenchmarkCase, ...],
    predictions: tuple[Prediction, ...],
) -> ScoreReport:
    prediction_by_id = {prediction.case_id: prediction for prediction in predictions}
    if set(prediction_by_id) != {case.case_id for case in cases}:
        raise ValueError("Scoring requires exactly one prediction for every benchmark case.")

    matrix = {expected: {actual: 0 for actual in LABELS} for expected in LABELS}
    critical_counts = {name: 0 for name in CRITICAL_PAIRS.values()}
    results: list[CaseResult] = []

    for case in cases:
        prediction = prediction_by_id[case.case_id]
        parsed = parse_model_output(prediction.raw_output)
        result = _score_case(case, prediction.raw_output, parsed)
        results.append(result)
        matrix[result.expected_label][result.actual_label] += 1
        if result.critical_confusion is not None:
            critical_counts[result.critical_confusion] += 1

    summary: dict[str, Any] = {
        "total": len(results),
        "passed": sum(result.passed for result in results),
        "failed": sum(not result.passed for result in results),
        "exactSemanticPasses": sum(
            result.passed and result.expected_label != "REJECT" for result in results
        ),
        "expectedRejectPasses": sum(
            result.passed and result.expected_label == "REJECT" for result in results
        ),
        "invalidOutputs": sum(result.actual_label == "INVALID" for result in results),
        "supportedCasesRejected": sum(
            result.failure_category == "SUPPORTED_CASE_REJECTED" for result in results
        ),
        "ambiguousCasesForced": sum(
            result.failure_category == "AMBIGUOUS_CASE_FORCED" for result in results
        ),
        "semanticMismatches": sum(
            result.failure_category == "SEMANTIC_MISMATCH" for result in results
        ),
        "criticalConfusions": critical_counts,
        "confusionMatrix": matrix,
    }
    return ScoreReport(results=tuple(results), summary=summary)


def _score_case(case: BenchmarkCase, raw_output: str, parsed: ModelOutput) -> CaseResult:
    expected_comparison = case.expected if isinstance(case.expected, Comparison) else None
    expected_label = expected_comparison.kind.value if expected_comparison else "REJECT"
    actual_comparison = parsed.semantic if isinstance(parsed.semantic, Comparison) else None
    actual_label = (
        actual_comparison.kind.value
        if actual_comparison is not None
        else "REJECT"
        if parsed.outcome == "reject"
        else "INVALID"
    )
    failure_category: str | None = None
    differences: tuple[str, ...] = ()

    if parsed.outcome == "invalid":
        passed = False
        failure_category = "INVALID_MODEL_OUTPUT"
    elif expected_comparison is not None:
        if parsed.outcome == "reject":
            passed = False
            failure_category = "SUPPORTED_CASE_REJECTED"
        elif actual_comparison == expected_comparison:
            passed = True
        else:
            passed = False
            failure_category = "SEMANTIC_MISMATCH"
            assert actual_comparison is not None
            differences = tuple(
                name
                for name, differs in (
                    ("comparisonKind", actual_comparison.kind != expected_comparison.kind),
                    ("leftOperand", actual_comparison.left != expected_comparison.left),
                    ("rightOperand", actual_comparison.right != expected_comparison.right),
                )
                if differs
            )
    else:
        if parsed.outcome == "reject":
            passed = True
        else:
            passed = False
            failure_category = "AMBIGUOUS_CASE_FORCED"

    critical_confusion = None
    if expected_comparison is not None and actual_comparison is not None:
        critical_confusion = CRITICAL_PAIRS.get(
            frozenset((expected_comparison.kind.value, actual_comparison.kind.value))
        )

    return CaseResult(
        case_id=case.case_id,
        expected_label=expected_label,
        actual_label=actual_label,
        passed=passed,
        failure_category=failure_category,
        critical_confusion=critical_confusion,
        semantic_differences=differences,
        expected_semantic=semantic_to_data(expected_comparison) if expected_comparison else None,
        actual_semantic=semantic_to_data(actual_comparison) if actual_comparison else None,
        raw_output=raw_output,
    )
