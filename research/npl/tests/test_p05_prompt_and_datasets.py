from __future__ import annotations

import hashlib
import unittest
from pathlib import Path

from vector_npl_research.p05_config import (
    FEW_SHOT_DEMONSTRATION_IDS,
    PROMPT_SHA256,
    load_p05_config,
)
from vector_npl_research.p05_datasets import P05DatasetError, resolve_p05_dataset
from vector_npl_research.flan_t5 import canonicalize_prompt_bytes, load_p05_prompt
from vector_npl_research.training_data import prepare_training_dataset


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
DATASET_ROOT = RESEARCH_ROOT / "datasets" / "comparisons"


class P05PromptTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.config = load_p05_config(RESEARCH_ROOT / "configs" / "p05_flan_t5_small.json")
        cls.training = prepare_training_dataset(
            DATASET_ROOT / "comparison_training.jsonl",
            DATASET_ROOT / "dataset_manifest.json",
        )

    def test_zero_shot_has_no_examples_and_inserts_input_deterministically(self) -> None:
        prompt = load_p05_prompt(RESEARCH_ROOT, self.config, "zero-shot", self.training.records)
        self.assertEqual("p05-comparison-v3", prompt.version)
        self.assertEqual("comparison_p05_v3.txt", prompt.template_path.name)
        rendered = prompt.render("unseen request")
        self.assertNotIn("Example 1", rendered)
        self.assertIn("Request: unseen request\nOutput:", rendered)
        self.assertEqual(rendered, prompt.render("unseen request"))
        self.assertNotIn("expected", rendered)

    def test_few_shot_uses_exact_authoritative_records_in_frozen_order(self) -> None:
        prompt = load_p05_prompt(RESEARCH_ROOT, self.config, "few-shot-2", self.training.records)
        self.assertEqual(FEW_SHOT_DEMONSTRATION_IDS, prompt.demonstration_ids)
        self.assertEqual(FEW_SHOT_DEMONSTRATION_IDS, tuple(r.case_id for r in prompt.demonstrations))
        rendered = prompt.render("new input")
        self.assertEqual(1, rendered.count("Example 1"))
        self.assertEqual(1, rendered.count("Example 2"))
        first, second = prompt.demonstrations
        first_text = f"Request: {first.input_text}\nOutput: {first.target_output}"
        second_text = f"Request: {second.input_text}\nOutput: {second.target_output}"
        self.assertIn(first_text, rendered)
        self.assertIn(second_text, rendered)
        self.assertLess(rendered.index(first_text), rendered.index(second_text))
        self.assertTrue(first.target_output.startswith('{"outcome":"semantic"'))

    def test_prompt_sha_is_exact_file_sha(self) -> None:
        prompt = load_p05_prompt(RESEARCH_ROOT, self.config, "zero-shot", self.training.records)
        self.assertEqual(
            hashlib.sha256(canonicalize_prompt_bytes(prompt.template_path.read_bytes())).hexdigest(),
            prompt.sha256,
        )
        self.assertEqual(PROMPT_SHA256, prompt.sha256)


class P05DatasetTests(unittest.TestCase):
    def test_p04_development_uses_validated_metadata_and_conversion(self) -> None:
        selection = resolve_p05_dataset(RESEARCH_ROOT, "p04-development")
        self.assertEqual(210, len(selection.cases))
        self.assertEqual("training-development", selection.metadata.dataset_role)
        self.assertEqual(
            "ac1ffd374434aead0ed635fa57f54730fab4a879ba7d623ab20ad19248c2a42f",
            selection.metadata.dataset_sha256,
        )
        prepared = prepare_training_dataset(
            DATASET_ROOT / "comparison_training_development.jsonl",
            DATASET_ROOT / "dataset_manifest.json",
        )
        record = prepared.records[0]
        case = selection.cases[0]
        self.assertEqual(record.case_id, case.case_id)
        self.assertEqual(record.input_text, case.input_text)
        self.assertEqual(record.expected, case.expected)
        self.assertEqual(record.reject_reason, case.reject_reason)
        self.assertEqual(record.tags, case.tags)
        self.assertEqual(record.pair, case.pair)

    def test_p02_visible_development_uses_existing_validator(self) -> None:
        selection = resolve_p05_dataset(RESEARCH_ROOT, "p02-development")
        self.assertEqual(60, len(selection.cases))
        self.assertEqual("development", selection.metadata.dataset_role)
        self.assertEqual(
            "42b64a251fb842b541f42921b5137c4956d92c7a7fd8cfb1ee6d596b568f15b7",
            selection.metadata.dataset_sha256,
        )

    def test_unknown_and_sealed_aliases_are_rejected(self) -> None:
        for alias in ("custom", "path", "p02-sealed", "sealed-gate"):
            with self.subTest(alias=alias), self.assertRaises(P05DatasetError):
                resolve_p05_dataset(RESEARCH_ROOT, alias)


if __name__ == "__main__":
    unittest.main()
