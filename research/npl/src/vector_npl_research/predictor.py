from __future__ import annotations

from collections.abc import Mapping
from typing import Protocol


class Predictor(Protocol):
    @property
    def model_id(self) -> str:
        ...

    @property
    def model_config(self) -> Mapping[str, object]:
        ...

    def predict(self, text: str) -> str:
        ...
