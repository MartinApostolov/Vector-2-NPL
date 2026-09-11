from __future__ import annotations

import json
import tempfile
import unittest
from copy import deepcopy
from pathlib import Path

from vector_npl_research.p05_config import (
    FEW_SHOT_DEMONSTRATION_IDS,
    MODEL_ID,
    MODEL_REVISION,
    P05ConfigError,
    load_p05_config,
)


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = RESEARCH_ROOT / "configs" / "p05_flan_t5_small.json"


class P05ConfigTests(unittest.TestCase):
    def test_frozen_config_loads(self) -> None:
        config = load_p05_config(CONFIG_PATH)
        self.assertEqual(MODEL_ID, config.model_id)
        self.assertEqual(MODEL_REVISION, config.model_revision)
        self.assertEqual("cpu", config.device)
        self.assertEqual("float32", config.dtype)
        self.assertTrue(config.use_safetensors)
        self.assertFalse(config.trust_remote_code)
        self.assertEqual("p05-comparison-v3", config.prompt_version)
        self.assertEqual("prompts/comparison_p05_v3.txt", config.prompt_template)
        self.assertFalse(config.do_sample)
        self.assertEqual(1, config.num_beams)
        self.assertEqual(128, config.max_new_tokens)
        self.assertEqual((), config.demonstration_ids("zero-shot"))
        self.assertEqual(
            FEW_SHOT_DEMONSTRATION_IDS,
            config.demonstration_ids("few-shot-2"),
        )

    def test_wrong_schema_phase_and_model_are_rejected(self) -> None:
        for path, value in (
            (("schemaVersion",), 2),
            (("phase",), "P06"),
            (("model", "id"), "other/model"),
        ):
            with self.subTest(path=path), self.assertRaises(P05ConfigError):
                self.load_mutation(path, value)

    def test_conditions_demonstrations_and_generation_are_frozen(self) -> None:
        mutations = (
            (("prompt", "conditions", "few-shot-2", "demonstrationIds"), []),
            (("prompt", "conditions", "zero-shot", "demonstrationIds"), ["x"]),
            (("generation", "doSample"), True),
            (("generation", "numBeams"), 2),
            (("generation", "maxNewTokens"), 64),
            (("model", "device"), "cuda"),
            (("prompt", "template"), "other.txt"),
        )
        for path, value in mutations:
            with self.subTest(path=path), self.assertRaises(P05ConfigError):
                self.load_mutation(path, value)

    def test_unknown_condition_is_rejected(self) -> None:
        config = load_p05_config(CONFIG_PATH)
        with self.assertRaises(P05ConfigError):
            config.demonstration_ids("few-shot-3")

    def load_mutation(self, path: tuple[str, ...], replacement: object):
        value = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        mutated = deepcopy(value)
        target = mutated
        for member in path[:-1]:
            target = target[member]
        target[path[-1]] = replacement
        with tempfile.TemporaryDirectory() as directory:
            config_path = Path(directory) / "config.json"
            config_path.write_text(json.dumps(mutated), encoding="utf-8")
            return load_p05_config(config_path)


if __name__ == "__main__":
    unittest.main()
