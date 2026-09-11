from __future__ import annotations

import json
import unittest

from vector_npl_research.benchmark import BenchmarkCase
from vector_npl_research.predictions import Prediction
from vector_npl_research.scoring import LABELS, score_predictions
from vector_npl_research.semantic import Comparison, ComparisonKind, Identifier, NumberLiteral


def semantic_raw(kind: str, identifier: str = "score", number: int = 10) -> str:
    return json.dumps(
        {
            "outcome": "semantic",
            "semantic": {
                "type": "comparison",
                "kind": kind,
                "left": {"type": "identifier", "name": identifier},
                "right": {"type": "numberLiteral", "value": number},
            },
        },
        separators=(",", ":"),
    )


def semantic_case(case_id: str, kind: ComparisonKind) -> BenchmarkCase:
    return BenchmarkCase(
        case_id=case_id,
        language="en",
        input_text="fixture",
        expectation="semantic",
        expected=Comparison(kind, Identifier("score"), NumberLiteral(10)),
        reject_reason=None,
        tags=(kind.value,),
        pair=None,
        notes=None,
    )


def reject_case(case_id: str) -> BenchmarkCase:
    return BenchmarkCase(
        case_id=case_id,
        language="en",
        input_text="ambiguous fixture",
        expectation="reject",
        expected=None,
        reject_reason="ambiguous",
        tags=("reject",),
        pair=None,
        notes=None,
    )


def score_one(case: BenchmarkCase, raw: str):
    return score_predictions((case,), (Prediction(case.case_id, raw),))


class ScoringTests(unittest.TestCase):
    def test_exact_semantic_match_passes(self) -> None:
        report = score_one(semantic_case("case", ComparisonKind.GTE), semantic_raw("GTE"))
        result = report.results[0]
        self.assertTrue(result.passed)
        self.assertIsNone(result.failure_category)
        self.assertEqual(1, report.summary["exactSemanticPasses"])

    def test_gt_gte_mismatch_is_critical_confusion(self) -> None:
        self.assert_critical_confusion(ComparisonKind.GT, "GTE", "GT<->GTE")

    def test_lt_lte_mismatch_is_critical_confusion(self) -> None:
        self.assert_critical_confusion(ComparisonKind.LTE, "LT", "LT<->LTE")

    def test_eq_neq_mismatch_is_critical_confusion(self) -> None:
        self.assert_critical_confusion(ComparisonKind.NEQ, "EQ", "EQ<->NEQ")

    def test_correct_kind_wrong_identifier_is_noncritical_semantic_mismatch(self) -> None:
        report = score_one(
            semantic_case("case", ComparisonKind.GT),
            semantic_raw("GT", identifier="other"),
        )
        result = report.results[0]
        self.assertFalse(result.passed)
        self.assertEqual("SEMANTIC_MISMATCH", result.failure_category)
        self.assertEqual(("leftOperand",), result.semantic_differences)
        self.assertIsNone(result.critical_confusion)

    def test_correct_kind_wrong_literal_is_noncritical_semantic_mismatch(self) -> None:
        report = score_one(
            semantic_case("case", ComparisonKind.GT),
            semantic_raw("GT", number=11),
        )
        result = report.results[0]
        self.assertFalse(result.passed)
        self.assertEqual(("rightOperand",), result.semantic_differences)
        self.assertIsNone(result.critical_confusion)

    def test_supported_case_rejection_fails(self) -> None:
        report = score_one(semantic_case("case", ComparisonKind.GT), '{"outcome":"reject"}')
        result = report.results[0]
        self.assertEqual("SUPPORTED_CASE_REJECTED", result.failure_category)
        self.assertEqual("REJECT", result.actual_label)
        self.assertEqual(1, report.summary["supportedCasesRejected"])

    def test_expected_reject_with_explicit_reject_passes(self) -> None:
        report = score_one(reject_case("case"), '{"outcome":"reject"}')
        self.assertTrue(report.results[0].passed)
        self.assertEqual(1, report.summary["expectedRejectPasses"])

    def test_expected_reject_with_semantics_is_ambiguous_forced(self) -> None:
        report = score_one(reject_case("case"), semantic_raw("EQ"))
        result = report.results[0]
        self.assertFalse(result.passed)
        self.assertEqual("AMBIGUOUS_CASE_FORCED", result.failure_category)
        self.assertEqual(1, report.summary["ambiguousCasesForced"])

    def test_invalid_output_fails_supported_and_rejection_cases(self) -> None:
        cases = (
            semantic_case("supported", ComparisonKind.GT),
            reject_case("reject"),
        )
        predictions = (
            Prediction("supported", "not json"),
            Prediction("reject", '{"outcome":"Reject"}'),
        )
        report = score_predictions(cases, predictions)
        self.assertTrue(
            all(result.failure_category == "INVALID_MODEL_OUTPUT" for result in report.results)
        )
        self.assertEqual(2, report.summary["invalidOutputs"])

    def test_confusion_matrix_is_complete_and_counts_actual_labels(self) -> None:
        cases = (
            semantic_case("exact", ComparisonKind.GT),
            semantic_case("rejected", ComparisonKind.GTE),
            reject_case("invalid"),
        )
        predictions = (
            Prediction("invalid", "prose"),
            Prediction("exact", semantic_raw("GT")),
            Prediction("rejected", '{"outcome":"reject"}'),
        )
        report = score_predictions(cases, predictions)
        matrix = report.summary["confusionMatrix"]
        self.assertEqual(list(LABELS), list(matrix))
        self.assertTrue(all(list(row) == list(LABELS) for row in matrix.values()))
        self.assertEqual(1, matrix["GT"]["GT"])
        self.assertEqual(1, matrix["GTE"]["REJECT"])
        self.assertEqual(1, matrix["REJECT"]["INVALID"])

    def test_prediction_order_does_not_change_results_or_summary(self) -> None:
        cases = (
            semantic_case("a", ComparisonKind.GT),
            reject_case("b"),
        )
        forward = (
            Prediction("a", semantic_raw("GT")),
            Prediction("b", '{"outcome":"reject"}'),
        )
        reverse = tuple(reversed(forward))
        self.assertEqual(score_predictions(cases, forward), score_predictions(cases, reverse))

    def assert_critical_confusion(
        self,
        expected: ComparisonKind,
        actual: str,
        confusion: str,
    ) -> None:
        report = score_one(semantic_case("case", expected), semantic_raw(actual))
        result = report.results[0]
        self.assertEqual("SEMANTIC_MISMATCH", result.failure_category)
        self.assertIn("comparisonKind", result.semantic_differences)
        self.assertEqual(confusion, result.critical_confusion)
        self.assertEqual(1, report.summary["criticalConfusions"][confusion])


if __name__ == "__main__":
    unittest.main()
