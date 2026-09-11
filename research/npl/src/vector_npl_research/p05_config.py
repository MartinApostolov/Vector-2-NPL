from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .semantic import SemanticValidationError, load_json_strict


MODEL_ID = "google/flan-t5-small"
MODEL_REVISION = "0fc9ddf78a1e988dac52e2dac162b0ede4fd74ab"
PROMPT_VERSION = "p05-comparison-v3"
PROMPT_TEMPLATE = "prompts/comparison_p05_v3.txt"
PROMPT_SHA256 = "743951694521965ff2d6c1de374057290ee3b2a918afa8fafaccedf9e5ad9987"
CANONICAL_SEED = 1234
MAX_INPUT_TOKENS = 512
P05_CONDITIONS = ("zero-shot", "few-shot-2")
P05_DATASETS = ("p04-development", "p02-development")
FEW_SHOT_DEMONSTRATION_IDS = (
    "p04-train-gt-01-01",
    "p04-train-gte-01-01",
)

_FROZEN_CONFIG: dict[str, Any] = {
    "schemaVersion": 1,
    "phase": "P05",
    "model": {
        "id": MODEL_ID,
        "revision": MODEL_REVISION,
        "device": "cpu",
        "dtype": "float32",
        "useSafetensors": True,
        "trustRemoteCode": False,
    },
    "prompt": {
        "version": PROMPT_VERSION,
        "template": PROMPT_TEMPLATE,
        "conditions": {
            "zero-shot": {"demonstrationIds": []},
            "few-shot-2": {"demonstrationIds": list(FEW_SHOT_DEMONSTRATION_IDS)},
        },
    },
    "generation": {
        "doSample": False,
        "numBeams": 1,
        "maxNewTokens": 128,
    },
    "canonicalSeed": CANONICAL_SEED,
}


class P05ConfigError(ValueError):
    pass


@dataclass(frozen=True, slots=True)
class P05Config:
    model_id: str
    model_revision: str
    device: str
    dtype: str
    use_safetensors: bool
    trust_remote_code: bool
    prompt_version: str
    prompt_template: str
    conditions: dict[str, tuple[str, ...]]
    do_sample: bool
    num_beams: int
    max_new_tokens: int
    canonical_seed: int

    def demonstration_ids(self, condition: str) -> tuple[str, ...]:
        try:
            return self.conditions[condition]
        except KeyError as error:
            raise P05ConfigError(
                f"Unsupported P05 condition {condition!r}; choose zero-shot or few-shot-2."
            ) from error


def load_p05_config(path: str | Path) -> P05Config:
    config_path = Path(path)
    try:
        value = load_json_strict(config_path.read_text(encoding="utf-8-sig"))
    except (OSError, SemanticValidationError) as error:
        raise P05ConfigError(f"Invalid P05 config: {error}") from error
    if not isinstance(value, dict):
        raise P05ConfigError("P05 config must be a JSON object.")
    _validate_frozen_config(value)
    model = value["model"]
    prompt = value["prompt"]
    generation = value["generation"]
    return P05Config(
        model_id=model["id"],
        model_revision=model["revision"],
        device=model["device"],
        dtype=model["dtype"],
        use_safetensors=model["useSafetensors"],
        trust_remote_code=model["trustRemoteCode"],
        prompt_version=prompt["version"],
        prompt_template=prompt["template"],
        conditions={
            name: tuple(details["demonstrationIds"])
            for name, details in prompt["conditions"].items()
        },
        do_sample=generation["doSample"],
        num_beams=generation["numBeams"],
        max_new_tokens=generation["maxNewTokens"],
        canonical_seed=value["canonicalSeed"],
    )


def _validate_frozen_config(value: dict[str, Any]) -> None:
    expected_top = set(_FROZEN_CONFIG)
    if set(value) != expected_top:
        _raise_members("config", expected_top, set(value))
    if value.get("schemaVersion") != 1:
        raise P05ConfigError("P05 config schemaVersion must be 1.")
    if value.get("phase") != "P05":
        raise P05ConfigError("P05 config phase must be exactly 'P05'.")

    for section in ("model", "prompt", "generation"):
        actual = value.get(section)
        expected = _FROZEN_CONFIG[section]
        if not isinstance(actual, dict):
            raise P05ConfigError(f"P05 config {section} must be an object.")
        assert isinstance(expected, dict)
        if set(actual) != set(expected):
            _raise_members(section, set(expected), set(actual))

    conditions = value["prompt"].get("conditions")
    expected_conditions = _FROZEN_CONFIG["prompt"]["conditions"]
    if not isinstance(conditions, dict) or set(conditions) != set(expected_conditions):
        raise P05ConfigError("P05 config must define exactly zero-shot and few-shot-2.")
    for name, expected_condition in expected_conditions.items():
        condition = conditions.get(name)
        if not isinstance(condition, dict) or set(condition) != {"demonstrationIds"}:
            raise P05ConfigError(f"P05 condition {name!r} has an invalid schema.")
        if condition != expected_condition:
            raise P05ConfigError(f"P05 condition {name!r} demonstration IDs are frozen.")

    for section in ("model", "generation"):
        if value[section] != _FROZEN_CONFIG[section]:
            raise P05ConfigError(f"P05 {section} settings do not match the frozen experiment.")
    if value["prompt"]["version"] != PROMPT_VERSION:
        raise P05ConfigError("P05 prompt version does not match the frozen experiment.")
    if value["prompt"]["template"] != PROMPT_TEMPLATE:
        raise P05ConfigError("P05 prompt template path does not match the frozen experiment.")
    if value["canonicalSeed"] != CANONICAL_SEED:
        raise P05ConfigError("P05 canonical seed must be 1234.")


def _raise_members(description: str, expected: set[str], actual: set[str]) -> None:
    missing = sorted(expected - actual)
    unknown = sorted(actual - expected)
    if missing:
        raise P05ConfigError(f"P05 {description} is missing member {missing[0]!r}.")
    raise P05ConfigError(f"P05 {description} contains unknown member {unknown[0]!r}.")
