from __future__ import annotations

import random


def validate_research_seed(seed: int) -> None:
    if not isinstance(seed, int) or isinstance(seed, bool):
        raise ValueError("random seed must be an integer.")


def apply_research_seed(seed: int) -> None:
    validate_research_seed(seed)
    random.seed(seed)
