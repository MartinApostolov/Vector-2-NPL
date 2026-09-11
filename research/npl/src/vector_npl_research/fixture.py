from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

from .artifacts import default_run_directory, write_run_artifacts
from .benchmark import prepare_benchmark
from .runner import generate_predictions
from .scoring import score_predictions


@dataclass(frozen=True, slots=True)
class FixedFixturePredictor:
    @property
    def model_id(self) -> str:
        return "p03-fixed-fixture"

    @property
    def model_config(self) -> Mapping[str, object]:
        return {"implementation": "fixed-input-map", "realModel": False}

    def predict(self, text: str) -> str:
        try:
            return _FIXED_OUTPUTS[text]
        except KeyError as error:
            raise ValueError("The fixed fixture predictor received an unknown synthetic input.") from error


_FIXED_OUTPUTS = {
    "score is greater than 10.": (
        '{"outcome":"semantic","semantic":{"type":"comparison","kind":"GT",'
        '"left":{"type":"identifier","name":"score"},'
        '"right":{"type":"numberLiteral","value":10}}}'
    ),
    "score is at least 10.": (
        '{"outcome":"semantic","semantic":{"type":"comparison","kind":"GTE",'
        '"left":{"type":"identifier","name":"score"},'
        '"right":{"type":"numberLiteral","value":10}}}'
    ),
    "score is around 10.": '{"outcome":"reject"}',
}


def run_synthetic_fixture(
    research_root: str | Path,
    repository_root: str | Path,
    *,
    output_directory: str | Path | None = None,
    seed: int = 1234,
) -> Path:
    root = Path(research_root)
    testdata = root / "testdata"
    selection = prepare_benchmark(
        testdata / "fixture_benchmark.jsonl",
        testdata / "fixture_manifest.json",
    )
    predictor = FixedFixturePredictor()
    predictions = generate_predictions(
        selection.cases,
        predictor,
        random_seed=seed,
    )
    report = score_predictions(selection.cases, predictions)
    if report.summary["failed"] != 0:
        raise RuntimeError("The fixed synthetic fixture unexpectedly failed.")
    destination = output_directory or default_run_directory(root, "fixture")
    return write_run_artifacts(
        destination,
        selection,
        predictions,
        report,
        model_id=predictor.model_id,
        model_config=predictor.model_config,
        random_seed=seed,
        parameters={"command": "run-fixture"},
        repository_root=repository_root,
    )
