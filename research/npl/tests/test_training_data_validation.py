from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from training_test_helpers import jsonl, reject_data, semantic_data
from vector_npl_research.training_data import (
    TrainingDataError,
    load_training_jsonl,
)


class TrainingRecordValidationTests(unittest.TestCase):
    def test_malformed_duplicate_and_nonobject_json_are_rejected(self) -> None:
        invalid_values = (
            '{"id":',
            '{"id":"one","id":"two"}',
            "[]",
            "\n",
        )
        for value in invalid_values:
            with self.subTest(value=value), self.assertRaises(TrainingDataError):
                self.load(value)

    def test_unknown_member_and_blank_required_strings_are_rejected(self) -> None:
        for member in ("id", "input", "family"):
            record = reject_data()
            record[member] = " "
            with self.subTest(member=member), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

        record = reject_data()
        record["extra"] = True
        with self.assertRaises(TrainingDataError):
            self.load(jsonl(record))

    def test_wrong_language_split_and_expectation_are_rejected(self) -> None:
        for member, value in (
            ("language", "bg"),
            ("split", "validation"),
            ("expectation", "maybe"),
        ):
            record = reject_data()
            record[member] = value
            with self.subTest(member=member), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

        record = reject_data(split="development")
        with self.assertRaises(TrainingDataError):
            self.load(jsonl(record), expected_split="train")

    def test_missing_empty_and_blank_tags_are_rejected(self) -> None:
        missing = reject_data()
        del missing["tags"]
        empty = reject_data()
        empty["tags"] = []
        blank = reject_data()
        blank["tags"] = ["REJECT", " "]
        nonstring = reject_data()
        nonstring["tags"] = ["REJECT", 1]
        for record in (missing, empty, blank, nonstring):
            with self.subTest(record=record), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

    def test_invalid_and_noncomparison_expected_ir_are_rejected(self) -> None:
        invalid = semantic_data()
        invalid["expected"] = {"type": "futureNode"}
        noncomparison = semantic_data()
        noncomparison["expected"] = {"type": "currentItem"}
        for record in (invalid, noncomparison):
            with self.subTest(record=record), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

    def test_semantic_record_shape_rules_are_strict(self) -> None:
        missing_expected = semantic_data()
        del missing_expected["expected"]
        with_reason = semantic_data()
        with_reason["rejectReason"] = "forbidden"
        missing_pair = semantic_data(pair=None)
        numeric_pair = semantic_data()
        numeric_pair["pair"] = 1
        blank_pair = semantic_data()
        blank_pair["pair"] = " "
        for record in (
            missing_expected,
            with_reason,
            missing_pair,
            numeric_pair,
            blank_pair,
        ):
            with self.subTest(record=record), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

    def test_reject_record_shape_rules_are_strict(self) -> None:
        with_expected = reject_data()
        with_expected["expected"] = {"type": "currentItem"}
        missing_reason = reject_data()
        del missing_reason["rejectReason"]
        blank_reason = reject_data()
        blank_reason["rejectReason"] = " "
        for record in (with_expected, missing_reason, blank_reason):
            with self.subTest(record=record), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

    def test_invalid_wrong_direction_and_mismatched_targets_are_rejected(self) -> None:
        malformed = semantic_data()
        malformed["targetOutput"] = "not json"
        semantic_as_reject = semantic_data()
        semantic_as_reject["targetOutput"] = '{"outcome":"reject"}'
        reject_as_semantic = reject_data()
        reject_as_semantic["targetOutput"] = semantic_data()["targetOutput"]
        wrong_tree = semantic_data()
        wrong_tree["targetOutput"] = semantic_data(kind="GTE")["targetOutput"]
        for record in (malformed, semantic_as_reject, reject_as_semantic, wrong_tree):
            with self.subTest(record=record), self.assertRaises(TrainingDataError):
                self.load(jsonl(record))

    def test_semantically_valid_noncanonical_target_is_rejected(self) -> None:
        record = semantic_data()
        parsed = json.loads(record["targetOutput"])
        record["targetOutput"] = json.dumps(parsed, indent=2)
        with self.assertRaisesRegex(TrainingDataError, "not canonical"):
            self.load(jsonl(record))

        reject = reject_data()
        reject["targetOutput"] = '{ "outcome": "reject" }'
        with self.assertRaisesRegex(TrainingDataError, "not canonical"):
            self.load(jsonl(reject))

    def test_duplicate_ids_and_normalized_inputs_are_rejected(self) -> None:
        first = reject_data(case_id="one")
        duplicate_id = reject_data(case_id="one")
        duplicate_id["input"] = "different"
        with self.assertRaisesRegex(TrainingDataError, "Duplicate"):
            self.load(jsonl(first, duplicate_id))

        duplicate_input = reject_data(case_id="two")
        duplicate_input["input"] = "  AMBIGUOUS INPUT FOR ONE  "
        with self.assertRaisesRegex(TrainingDataError, "duplicates"):
            self.load(jsonl(first, duplicate_input))

    def load(self, text: str, *, expected_split: str = "train"):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "data.jsonl"
            path.write_text(text, encoding="utf-8", newline="\n")
            return load_training_jsonl(path, expected_split=expected_split)


if __name__ == "__main__":
    unittest.main()
