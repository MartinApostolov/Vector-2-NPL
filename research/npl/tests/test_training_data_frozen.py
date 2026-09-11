from __future__ import annotations

import unittest
from pathlib import Path

from vector_npl_research.model_output import parse_model_output
from vector_npl_research.training_data import (
    canonical_target_output,
    count_dispositions,
    count_pair_groups,
    find_training_split_leaks,
    find_visible_development_leaks,
    prepare_training_dataset,
    validate_against_p02_visible_development,
    validate_training_splits,
    verify_training_manifest,
)
from vector_npl_research.benchmark import load_benchmark_jsonl


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
DATASET_ROOT = RESEARCH_ROOT / "datasets" / "comparisons"
VISIBLE_P02 = RESEARCH_ROOT / "benchmarks" / "comparisons" / "comparison_development.jsonl"


class FrozenTrainingDataTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.training, cls.development = verify_training_manifest(
            DATASET_ROOT / "dataset_manifest.json",
            DATASET_ROOT,
        )

    def test_training_file_loads_with_frozen_count_distribution_and_hash(self) -> None:
        self.assertEqual(1260, self.training.case_count)
        self.assertEqual(
            {
                "EQ": 180,
                "GT": 180,
                "GTE": 180,
                "LT": 180,
                "LTE": 180,
                "NEQ": 180,
                "REJECT": 180,
            },
            count_dispositions(self.training.records),
        )
        self.assertEqual(
            "32c44fbb4c29618c9b1e680a9140a9ae1d55efb81d9e173dcce04f364fff1b9d",
            self.training.dataset_sha256,
        )

    def test_training_development_loads_with_frozen_count_distribution_and_hash(self) -> None:
        self.assertEqual(210, self.development.case_count)
        self.assertEqual(
            {
                "EQ": 30,
                "GT": 30,
                "GTE": 30,
                "LT": 30,
                "LTE": 30,
                "NEQ": 30,
                "REJECT": 30,
            },
            count_dispositions(self.development.records),
        )
        self.assertEqual(
            "ac1ffd374434aead0ed635fa57f54730fab4a879ba7d623ab20ad19248c2a42f",
            self.development.dataset_sha256,
        )

    def test_manifest_roles_and_model_output_contract_verify(self) -> None:
        self.assertEqual("training", self.training.dataset_role)
        self.assertEqual("train", self.training.split)
        self.assertEqual("training-development", self.development.dataset_role)
        self.assertEqual("development", self.development.split)
        self.assertEqual("vector-npl-comparison-training-v1", self.training.dataset)

    def test_every_target_is_valid_exact_and_canonical(self) -> None:
        for record in (*self.training.records, *self.development.records):
            with self.subTest(case_id=record.case_id):
                parsed = parse_model_output(record.target_output)
                self.assertTrue(parsed.valid)
                self.assertEqual(canonical_target_output(record.expected), record.target_output)
                if record.expected is not None:
                    self.assertEqual("semantic", parsed.outcome)
                    self.assertEqual(record.expected, parsed.semantic)
                else:
                    self.assertEqual("reject", parsed.outcome)
                    self.assertEqual('{"outcome":"reject"}', record.target_output)

    def test_frozen_hard_negative_pairs_are_valid_with_expected_group_counts(self) -> None:
        self.assertEqual(540, count_pair_groups(self.training.records))
        self.assertEqual(90, count_pair_groups(self.development.records))
        self.assertTrue(
            all(record.pair for record in self.training.records if record.expected is not None)
        )
        self.assertTrue(
            all(record.pair for record in self.development.records if record.expected is not None)
        )

    def test_training_and_development_have_no_required_leakage(self) -> None:
        self.assertEqual(
            (),
            find_training_split_leaks(self.training.records, self.development.records),
        )
        validate_training_splits(self.training.records, self.development.records)

    def test_p04_has_no_leakage_with_only_visible_p02_development(self) -> None:
        visible = load_benchmark_jsonl(VISIBLE_P02)
        combined = (*self.training.records, *self.development.records)
        self.assertEqual((), find_visible_development_leaks(combined, visible))
        validate_against_p02_visible_development(combined, VISIBLE_P02)

    def test_individual_prepare_api_is_reusable(self) -> None:
        prepared = prepare_training_dataset(
            DATASET_ROOT / "comparison_training_development.jsonl",
            DATASET_ROOT / "dataset_manifest.json",
        )
        self.assertEqual(self.development, prepared)


if __name__ == "__main__":
    unittest.main()
