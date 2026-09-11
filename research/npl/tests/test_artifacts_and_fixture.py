from __future__ import annotations

import json
import random
import tempfile
import unittest
from pathlib import Path

from vector_npl_research.artifacts import GitState, write_run_artifacts
from vector_npl_research.benchmark import prepare_benchmark
from vector_npl_research.fixture import FixedFixturePredictor, run_synthetic_fixture
from vector_npl_research.predictions import Prediction
from vector_npl_research.runner import generate_predictions
from vector_npl_research.scoring import score_predictions


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = RESEARCH_ROOT.parents[1]


class ArtifactTests(unittest.TestCase):
    def setUp(self) -> None:
        testdata = RESEARCH_ROOT / "testdata"
        self.selection = prepare_benchmark(
            testdata / "fixture_benchmark.jsonl",
            testdata / "fixture_manifest.json",
        )
        predictor = FixedFixturePredictor()
        self.predictions = tuple(
            Prediction(case.case_id, predictor.predict(case.input_text))
            for case in self.selection.cases
        )
        self.report = score_predictions(self.selection.cases, self.predictions)

    def test_run_artifacts_are_valid_and_preserve_reproducibility_metadata(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            destination = write_run_artifacts(
                Path(directory) / "run",
                self.selection,
                tuple(reversed(self.predictions)),
                self.report,
                model_id="controlled-fixture",
                model_config={"temperature": 0},
                random_seed=77,
                parameters={"mode": "test"},
                repository_root=REPOSITORY_ROOT,
                run_id="controlled-run",
                created_at_utc="2026-09-11T12:00:00Z",
                git_state=GitState(commit="abc123", dirty=True),
            )

            self.assertEqual(
                {"run.json", "predictions.jsonl", "results.jsonl", "summary.json"},
                {path.name for path in destination.iterdir()},
            )
            run = json.loads((destination / "run.json").read_text(encoding="utf-8"))
            self.assertEqual("controlled-run", run["runId"])
            self.assertEqual("abc123", run["gitCommit"])
            self.assertTrue(run["gitDirty"])
            self.assertEqual(77, run["randomSeed"])
            self.assertEqual("development", run["datasetRole"])
            self.assertEqual(3, run["caseCount"])
            self.assertEqual({"temperature": 0}, run["modelConfig"])

            prediction_records = self.read_jsonl(destination / "predictions.jsonl")
            result_records = self.read_jsonl(destination / "results.jsonl")
            summary = json.loads((destination / "summary.json").read_text(encoding="utf-8"))
            self.assertEqual(["fixture-gt", "fixture-gte", "fixture-reject"], [
                item["caseId"] for item in prediction_records
            ])
            self.assertEqual(3, len(result_records))
            self.assertTrue(all("rawOutput" in item for item in result_records))
            self.assertEqual(3, summary["passed"])

    def test_raw_output_is_preserved_exactly_in_both_artifacts(self) -> None:
        raw = '{ "outcome" : "reject" }\n'
        reject_case = self.selection.cases[-1]
        predictions = tuple(
            Prediction(case.case_id, raw if case == reject_case else prediction.raw_output)
            for case, prediction in zip(self.selection.cases, self.predictions, strict=True)
        )
        report = score_predictions(self.selection.cases, predictions)
        with tempfile.TemporaryDirectory() as directory:
            destination = write_run_artifacts(
                Path(directory) / "run",
                self.selection,
                predictions,
                report,
                model_id="raw-preservation",
                model_config={},
                random_seed=1,
                parameters={},
                repository_root=REPOSITORY_ROOT,
                git_state=GitState(commit="commit", dirty=False),
            )
            prediction = next(
                item for item in self.read_jsonl(destination / "predictions.jsonl")
                if item["caseId"] == reject_case.case_id
            )
            result = next(
                item for item in self.read_jsonl(destination / "results.jsonl")
                if item["caseId"] == reject_case.case_id
            )
            self.assertEqual(raw, prediction["rawOutput"])
            self.assertEqual(raw, result["rawOutput"])

    def test_fixed_predictor_smoke_run_creates_all_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            destination = run_synthetic_fixture(
                RESEARCH_ROOT,
                REPOSITORY_ROOT,
                output_directory=Path(directory) / "fixture-run",
                seed=1234,
            )
            self.assertEqual(
                {"run.json", "predictions.jsonl", "results.jsonl", "summary.json"},
                {path.name for path in destination.iterdir()},
            )
            summary = json.loads((destination / "summary.json").read_text(encoding="utf-8"))
            self.assertEqual(3, summary["passed"])
            self.assertEqual(0, summary["failed"])

    def test_seed_is_applied_before_predictor_calls(self) -> None:
        first = RandomRecordingPredictor()
        second = RandomRecordingPredictor()
        different = RandomRecordingPredictor()

        generate_predictions(self.selection.cases, first, random_seed=1234)
        generate_predictions(self.selection.cases, second, random_seed=1234)
        generate_predictions(self.selection.cases, different, random_seed=4321)

        self.assertEqual(first.samples, second.samples)
        self.assertNotEqual(first.samples, different.samples)
        self.assertEqual(random.Random(1234).random(), first.samples[0])

    @staticmethod
    def read_jsonl(path: Path) -> list[dict[str, object]]:
        return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
class RandomRecordingPredictor:
    def __init__(self) -> None:
        self.samples: list[float] = []

    @property
    def model_id(self) -> str:
        return "random-recording-test"

    @property
    def model_config(self) -> dict[str, object]:
        return {"realModel": False}

    def predict(self, text: str) -> str:
        self.samples.append(random.random())
        return '{"outcome":"reject"}'


if __name__ == "__main__":
    unittest.main()
