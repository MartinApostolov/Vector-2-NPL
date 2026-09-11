from __future__ import annotations

import unittest

from training_test_helpers import reject_record, semantic_record
from vector_npl_research.benchmark import BenchmarkCase
from vector_npl_research.semantic import Comparison, ComparisonKind, Identifier, NumberLiteral
from vector_npl_research.training_data import (
    TrainingDataError,
    find_training_split_leaks,
    find_visible_development_leaks,
    validate_pair_integrity,
)


class PairIntegrityTests(unittest.TestCase):
    def test_valid_critical_pairs_pass(self) -> None:
        records = (
            semantic_record("gt", ComparisonKind.GT, "upper"),
            semantic_record("gte", ComparisonKind.GTE, "upper"),
            semantic_record("lt", ComparisonKind.LT, "lower"),
            semantic_record("lte", ComparisonKind.LTE, "lower"),
            semantic_record("eq", ComparisonKind.EQ, "equality"),
            semantic_record("neq", ComparisonKind.NEQ, "equality"),
            reject_record("reject"),
        )
        validate_pair_integrity(records)

    def test_missing_one_member_and_three_member_pairs_are_rejected(self) -> None:
        missing = (semantic_record("missing", ComparisonKind.GT, None),)
        one = (semantic_record("one", ComparisonKind.GT, "pair"),)
        three = (
            semantic_record("one", ComparisonKind.GT, "pair"),
            semantic_record("two", ComparisonKind.GTE, "pair"),
            semantic_record("three", ComparisonKind.GT, "pair"),
        )
        for records in (missing, one, three):
            with self.subTest(records=records), self.assertRaises(TrainingDataError):
                validate_pair_integrity(records)

    def test_wrong_kind_pair_is_rejected(self) -> None:
        records = (
            semantic_record("gt", ComparisonKind.GT, "pair"),
            semantic_record("lt", ComparisonKind.LT, "pair"),
        )
        with self.assertRaisesRegex(TrainingDataError, "near-neighbor"):
            validate_pair_integrity(records)

    def test_differing_left_operand_is_rejected(self) -> None:
        records = (
            semantic_record("gt", ComparisonKind.GT, "pair", identifier="score"),
            semantic_record("gte", ComparisonKind.GTE, "pair", identifier="other"),
        )
        with self.assertRaisesRegex(TrainingDataError, "left operands"):
            validate_pair_integrity(records)

    def test_differing_right_operand_is_rejected(self) -> None:
        records = (
            semantic_record("gt", ComparisonKind.GT, "pair", number=10),
            semantic_record("gte", ComparisonKind.GTE, "pair", number=11),
        )
        with self.assertRaisesRegex(TrainingDataError, "right operands"):
            validate_pair_integrity(records)


class LeakageTests(unittest.TestCase):
    def test_each_train_development_leak_detector_finds_synthetic_overlap(self) -> None:
        training = (
            semantic_record(
                "shared-id",
                ComparisonKind.GT,
                "train-pair",
                family="shared-family",
                input_text="score must exceed 10.",
            ),
        )
        development = (
            semantic_record(
                "shared-id",
                ComparisonKind.GTE,
                "dev-pair",
                family="shared-family",
                input_text="  SCORE must EXCEED 10. ",
                split="development",
            ),
        )
        codes = {leak.code for leak in find_training_split_leaks(training, development)}
        self.assertEqual(
            {
                "P04_ID_OVERLAP",
                "P04_INPUT_OVERLAP",
                "P04_FAMILY_OVERLAP",
                "P04_TEMPLATE_OVERLAP",
            },
            codes,
        )

        template_development = (
            semantic_record(
                "template-dev",
                ComparisonKind.GT,
                "dev-template",
                identifier="temperature",
                number=25,
                input_text="temperature must exceed 25.",
                split="development",
            ),
        )
        template_codes = {
            leak.code for leak in find_training_split_leaks(training, template_development)
        }
        self.assertIn("P04_TEMPLATE_OVERLAP", template_codes)

    def test_visible_p02_exact_and_template_detectors_find_synthetic_overlap(self) -> None:
        p04 = (
            semantic_record(
                "p04",
                ComparisonKind.GT,
                "pair",
                input_text="score must exceed 10.",
            ),
        )
        exact = BenchmarkCase(
            case_id="p02-exact",
            language="en",
            input_text=" SCORE must exceed 10. ",
            expectation="semantic",
            expected=Comparison(
                ComparisonKind.GT,
                Identifier("score"),
                NumberLiteral(10),
            ),
            reject_reason=None,
            tags=("GT",),
            pair=None,
            notes=None,
        )
        template = BenchmarkCase(
            case_id="p02-template",
            language="en",
            input_text="temperature must exceed 25.",
            expectation="semantic",
            expected=Comparison(
                ComparisonKind.GT,
                Identifier("temperature"),
                NumberLiteral(25),
            ),
            reject_reason=None,
            tags=("GT",),
            pair=None,
            notes=None,
        )
        codes = {
            leak.code for leak in find_visible_development_leaks(p04, (exact, template))
        }
        self.assertIn("P02_VISIBLE_INPUT_OVERLAP", codes)
        self.assertIn("P02_VISIBLE_TEMPLATE_OVERLAP", codes)


if __name__ == "__main__":
    unittest.main()
