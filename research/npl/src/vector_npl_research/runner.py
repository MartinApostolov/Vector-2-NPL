from __future__ import annotations

from collections.abc import Iterable

from .benchmark import BenchmarkCase
from .predictions import Prediction
from .predictor import Predictor
from .reproducibility import apply_research_seed


def generate_predictions(
    cases: Iterable[BenchmarkCase],
    predictor: Predictor,
    *,
    random_seed: int,
) -> tuple[Prediction, ...]:
    apply_research_seed(random_seed)
    return tuple(
        Prediction(case_id=case.case_id, raw_output=predictor.predict(case.input_text))
        for case in cases
    )
