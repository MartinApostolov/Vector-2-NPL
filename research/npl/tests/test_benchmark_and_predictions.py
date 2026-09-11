from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from vector_npl_research.benchmark import (
    BenchmarkError,
    SealedGateBlockedError,
    canonical_sha256,
    prepare_benchmark,
)
from vector_npl_research.predictions import (
    PredictionError,
    load_prediction_jsonl,
)


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = RESEARCH_ROOT.parents[1]
FIXTURE_DATASET = RESEARCH_ROOT / "testdata" / "fixture_benchmark.jsonl"
FIXTURE_MANIFEST = RESEARCH_ROOT / "testdata" / "fixture_manifest.json"


class BenchmarkTests(unittest.TestCase):
    def test_real_development_benchmark_hash_and_role_verify(self) -> None:
        benchmark_root = RESEARCH_ROOT / "benchmarks" / "comparisons"
        selection = prepare_benchmark(
            benchmark_root / "comparison_development.jsonl",
            benchmark_root / "benchmark_manifest.json",
        )
        self.assertEqual("development", selection.metadata.dataset_role)
        self.assertEqual(60, selection.metadata.case_count)
        self.assertEqual(
            "42b64a251fb842b541f42921b5137c4956d92c7a7fd8cfb1ee6d596b568f15b7",
            selection.metadata.dataset_sha256,
        )

    def test_canonical_hash_ignores_bom_and_line_endings(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            first = Path(directory) / "first.jsonl"
            second = Path(directory) / "second.jsonl"
            first.write_bytes(b"one\ntwo\n")
            second.write_bytes(b"\xef\xbb\xbfone\r\ntwo\r\n\r\n")
            self.assertEqual(canonical_sha256(first), canonical_sha256(second))

    def test_hash_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            manifest = json.loads(FIXTURE_MANIFEST.read_text(encoding="utf-8"))
            manifest["files"]["fixture_benchmark.jsonl"]["sha256"] = "0" * 64
            manifest_path = Path(directory) / "manifest.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(BenchmarkError):
                prepare_benchmark(FIXTURE_DATASET, manifest_path)

    def test_synthetic_sealed_role_is_blocked_by_default(self) -> None:
        manifest_path = self.synthetic_sealed_manifest()
        with self.assertRaises(SealedGateBlockedError):
            prepare_benchmark(FIXTURE_DATASET, manifest_path)

    def test_synthetic_sealed_role_can_be_explicitly_unlocked(self) -> None:
        manifest_path = self.synthetic_sealed_manifest()
        selection = prepare_benchmark(
            FIXTURE_DATASET,
            manifest_path,
            allow_sealed_gate=True,
        )
        self.assertEqual("sealed-gate", selection.metadata.dataset_role)
        self.assertEqual(3, len(selection.cases))

    def synthetic_sealed_manifest(self) -> Path:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        manifest = json.loads(FIXTURE_MANIFEST.read_text(encoding="utf-8"))
        manifest["files"]["fixture_benchmark.jsonl"]["role"] = "sealed-gate"
        path = Path(temporary.name) / "manifest.json"
        path.write_text(json.dumps(manifest), encoding="utf-8")
        return path


class PredictionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.case_ids = frozenset({"case-a", "case-b"})
        self.first = '{"caseId":"case-a","rawOutput":"{\\"outcome\\":\\"reject\\"}"}'
        self.second = '{"caseId":"case-b","rawOutput":"raw text"}'

    def test_valid_prediction_jsonl_is_order_independent(self) -> None:
        forward = load_prediction_jsonl(self.first + "\n" + self.second, self.case_ids)
        reverse = load_prediction_jsonl(self.second + "\n" + self.first, self.case_ids)
        self.assertEqual(forward, reverse)
        self.assertEqual("raw text", reverse[1].raw_output)

    def test_duplicate_case_ids_are_rejected(self) -> None:
        with self.assertRaises(PredictionError):
            load_prediction_jsonl(self.first + "\n" + self.first, frozenset({"case-a"}))

    def test_duplicate_json_members_are_rejected(self) -> None:
        duplicate = '{"caseId":"case-a","caseId":"case-b","rawOutput":"x"}'
        with self.assertRaises(PredictionError):
            load_prediction_jsonl(duplicate, frozenset({"case-a", "case-b"}))

    def test_unknown_and_missing_case_ids_are_rejected(self) -> None:
        unknown = '{"caseId":"other","rawOutput":"x"}'
        with self.assertRaises(PredictionError):
            load_prediction_jsonl(unknown, self.case_ids)
        with self.assertRaises(PredictionError):
            load_prediction_jsonl(self.first, self.case_ids)

    def test_unknown_prediction_members_are_rejected(self) -> None:
        invalid = '{"caseId":"case-a","rawOutput":"x","extra":true}'
        with self.assertRaises(PredictionError):
            load_prediction_jsonl(invalid, frozenset({"case-a"}))

    def test_case_id_must_be_nonblank_and_raw_output_must_be_string(self) -> None:
        with self.assertRaises(PredictionError):
            load_prediction_jsonl('{"caseId":" ","rawOutput":"x"}', frozenset({" "}))
        with self.assertRaises(PredictionError):
            load_prediction_jsonl('{"caseId":"case-a","rawOutput":1}', frozenset({"case-a"}))


if __name__ == "__main__":
    unittest.main()
