from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from vector_npl_research.benchmark import BenchmarkCase
from vector_npl_research.cli import build_parser, main
from vector_npl_research.flan_t5 import (
    FlanT5Dependencies,
    FlanT5Predictor,
    P05DependencyError,
    P05ModelCacheError,
    P05PromptError,
    cache_p05_model,
    load_p05_dependencies,
    load_p05_prompt,
)
from vector_npl_research.p05_config import MODEL_ID, MODEL_REVISION, load_p05_config
from vector_npl_research.p05 import run_p05_baseline
from vector_npl_research.training_data import prepare_training_dataset


RESEARCH_ROOT = Path(__file__).resolve().parents[1]


class FakeTensor:
    def __init__(self, length: int) -> None:
        self.shape = (1, length)
        self.moves: list[str] = []

    def to(self, device: str):
        self.moves.append(device)
        return self


class FakeTokenizer:
    def __init__(self, token_count: int, decoded: str = "  raw output  \n") -> None:
        self.token_count = token_count
        self.decoded = decoded
        self.model_max_length = 512
        self.calls: list[tuple[str, dict[str, object]]] = []
        self.decode_calls: list[tuple[object, dict[str, object]]] = []

    def __call__(self, text: str, **kwargs):
        self.calls.append((text, kwargs))
        return {"input_ids": FakeTensor(self.token_count), "attention_mask": FakeTensor(self.token_count)}

    def decode(self, tokens, **kwargs):
        self.decode_calls.append((tokens, kwargs))
        return self.decoded


class FakeModel:
    def __init__(self) -> None:
        self.to_calls: list[dict[str, object]] = []
        self.generate_calls: list[dict[str, object]] = []
        self.eval_called = False
        self.config = type(
            "Config",
            (),
            {"architectures": ["T5ForConditionalGeneration"], "model_type": "t5", "vocab_size": 32128, "d_model": 512},
        )()

    def to(self, **kwargs):
        self.to_calls.append(kwargs)
        return self

    def eval(self):
        self.eval_called = True
        return self

    def generate(self, **kwargs):
        self.generate_calls.append(kwargs)
        return [[101, 102]]


class FakeFactory:
    def __init__(self, value: object) -> None:
        self.value = value
        self.calls: list[tuple[str, dict[str, object]]] = []

    def from_pretrained(self, model_id: str, **kwargs):
        self.calls.append((model_id, kwargs))
        return self.value


class FailingFactory:
    @staticmethod
    def from_pretrained(model_id: str, **kwargs):
        raise OSError("not cached")


class FakeTorch:
    float32 = "float32"

    def __init__(self) -> None:
        self.seeds: list[int] = []

    def manual_seed(self, seed: int) -> None:
        self.seeds.append(seed)

    @staticmethod
    def inference_mode():
        return contextlib.nullcontext()


class P05PredictorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.config = load_p05_config(RESEARCH_ROOT / "configs" / "p05_flan_t5_small.json")
        dataset_root = RESEARCH_ROOT / "datasets" / "comparisons"
        training = prepare_training_dataset(
            dataset_root / "comparison_training.jsonl",
            dataset_root / "dataset_manifest.json",
        )
        cls.prompt = load_p05_prompt(RESEARCH_ROOT, cls.config, "zero-shot", training.records)

    def make_predictor(self, token_count: int = 512):
        tokenizer = FakeTokenizer(token_count)
        model = FakeModel()
        torch = FakeTorch()
        tokenizer_factory = FakeFactory(tokenizer)
        model_factory = FakeFactory(model)
        dependencies = FlanT5Dependencies(torch, tokenizer_factory, model_factory)
        predictor = FlanT5Predictor(
            self.config,
            self.prompt,
            random_seed=1234,
            dependencies=dependencies,
        )
        return predictor, tokenizer, model, torch, tokenizer_factory, model_factory

    def test_frozen_cache_only_loading_cpu_float32_and_greedy_generation(self) -> None:
        predictor, tokenizer, model, torch, tokenizer_factory, model_factory = self.make_predictor()
        raw = predictor.predict("score exceeds 1")
        self.assertEqual("  raw output  \n", raw)
        self.assertEqual([1234], torch.seeds)
        for factory in (tokenizer_factory, model_factory):
            model_id, kwargs = factory.calls[0]
            self.assertEqual(MODEL_ID, model_id)
            self.assertEqual(MODEL_REVISION, kwargs["revision"])
            self.assertTrue(kwargs["local_files_only"])
            self.assertFalse(kwargs["trust_remote_code"])
        self.assertTrue(model_factory.calls[0][1]["use_safetensors"])
        self.assertEqual("float32", model_factory.calls[0][1]["dtype"])
        self.assertEqual({"device": "cpu", "dtype": "float32"}, model.to_calls[0])
        self.assertTrue(model.eval_called)
        tokenizer_kwargs = tokenizer.calls[0][1]
        self.assertFalse(tokenizer_kwargs["truncation"])
        generation = model.generate_calls[0]
        self.assertFalse(generation["do_sample"])
        self.assertEqual(1, generation["num_beams"])
        self.assertEqual(128, generation["max_new_tokens"])
        self.assertEqual(
            {"skip_special_tokens": True, "clean_up_tokenization_spaces": False},
            tokenizer.decode_calls[0][1],
        )
        self.assertEqual(512, predictor.maximum_input_tokens)

    def test_overlong_prompt_fails_without_generation_or_truncation(self) -> None:
        predictor, tokenizer, model, *_ = self.make_predictor(513)
        with self.assertRaisesRegex(P05PromptError, "without truncation"):
            predictor.predict("input")
        self.assertEqual([], model.generate_calls)
        self.assertFalse(tokenizer.calls[0][1]["truncation"])

    def test_whole_run_preflight_names_overlong_case_before_model_loading(self) -> None:
        predictor, tokenizer, model, _, _, model_factory = self.make_predictor(513)
        cases = (
            BenchmarkCase(
                case_id="overlong-case",
                language="en",
                input_text="input",
                expectation="reject",
                expected=None,
                reject_reason="test",
                tags=("REJECT",),
                pair=None,
                notes=None,
            ),
        )
        with self.assertRaises(P05PromptError) as raised:
            predictor.preflight(
                cases,
                condition="few-shot-2",
                dataset_alias="p04-development",
            )
        message = str(raised.exception)
        self.assertIn("overlong-case", message)
        self.assertIn("513", message)
        self.assertIn("512", message)
        self.assertIn("few-shot-2", message)
        self.assertIn("p04-development", message)
        self.assertEqual([], model_factory.calls)
        self.assertEqual([], model.generate_calls)
        self.assertFalse(tokenizer.calls[0][1]["truncation"])
        self.assertEqual(513, predictor.maximum_input_tokens)

    def test_missing_cached_revision_error_names_cache_command(self) -> None:
        dependencies = FlanT5Dependencies(FakeTorch(), FailingFactory(), FailingFactory())
        with self.assertRaisesRegex(P05ModelCacheError, "cache-p05-model"):
            FlanT5Predictor(
                self.config,
                self.prompt,
                random_seed=1234,
                dependencies=dependencies,
            )

    def test_cache_command_allows_download_but_keeps_frozen_revision_and_safetensors(self) -> None:
        tokenizer_factory = FakeFactory(FakeTokenizer(1))
        model_factory = FakeFactory(FakeModel())
        cache_p05_model(
            self.config,
            dependencies=FlanT5Dependencies(FakeTorch(), tokenizer_factory, model_factory),
        )
        self.assertNotIn("local_files_only", tokenizer_factory.calls[0][1])
        self.assertNotIn("local_files_only", model_factory.calls[0][1])
        self.assertEqual(MODEL_REVISION, model_factory.calls[0][1]["revision"])
        self.assertTrue(model_factory.calls[0][1]["use_safetensors"])

    def test_missing_optional_dependency_error_is_actionable(self) -> None:
        with patch("vector_npl_research.flan_t5.importlib.import_module", side_effect=ImportError("missing")):
            with self.assertRaisesRegex(P05DependencyError, r"\[p05\]"):
                load_p05_dependencies()


class P05CliTests(unittest.TestCase):
    def test_legal_condition_and_dataset_parse(self) -> None:
        arguments = build_parser().parse_args(
            [
                "run-p05-baseline",
                "--condition", "few-shot-2",
                "--dataset", "p04-development",
                "--output-dir", "run",
            ]
        )
        self.assertEqual("few-shot-2", arguments.condition)
        self.assertEqual("p04-development", arguments.dataset)
        self.assertEqual(1234, arguments.seed)

    def test_illegal_condition_and_dataset_are_rejected(self) -> None:
        for option, value in (("--condition", "few-shot-3"), ("--dataset", "sealed-gate")):
            arguments = [
                "run-p05-baseline",
                "--condition", "zero-shot",
                "--dataset", "p02-development",
                "--output-dir", "run",
            ]
            arguments[arguments.index(option) + 1] = value
            with self.subTest(option=option), contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit):
                    build_parser().parse_args(arguments)

    def test_p05_cli_exposes_no_sealed_model_or_generation_overrides(self) -> None:
        help_text = build_parser().format_help()
        p05_parser = next(
            action.choices["run-p05-baseline"]
            for action in build_parser()._actions
            if getattr(action, "choices", None) and "run-p05-baseline" in action.choices
        )
        p05_help = p05_parser.format_help()
        for forbidden in (
            "--allow-sealed-gate",
            "--model-id",
            "--revision",
            "--prompt",
            "--max-new-tokens",
            "--num-beams",
        ):
            self.assertNotIn(forbidden, p05_help)
        self.assertIn("run-p05-baseline", help_text)

    def test_p05_dependency_error_is_reported_without_traceback(self) -> None:
        with patch(
            "vector_npl_research.cli.cache_frozen_p05_model",
            side_effect=P05DependencyError("install .[p05]"),
        ), contextlib.redirect_stderr(io.StringIO()) as errors:
            self.assertEqual(2, main(["cache-p05-model"]))
        self.assertIn("install .[p05]", errors.getvalue())


class FakeBaselinePredictor:
    def __init__(self) -> None:
        self.calls: list[str] = []

    model_id = MODEL_ID
    maximum_input_tokens = 42

    @property
    def model_config(self):
        return {
            "revision": MODEL_REVISION,
            "device": "cpu",
            "dtype": "float32",
            "maximumRenderedInputTokenCount": 42,
        }

    def preflight(self, cases, *, condition: str, dataset_alias: str) -> int:
        self.calls.append("preflight")
        tuple(cases)
        return 42

    def load_model(self) -> None:
        self.calls.append("load-model")

    def predict(self, text: str) -> str:
        self.calls.append("predict")
        return '{"outcome":"reject"}'


class P05OrchestrationTests(unittest.TestCase):
    def test_no_model_run_reuses_p03_artifacts_and_records_frozen_metadata(self) -> None:
        repository_root = RESEARCH_ROOT.parents[1]
        fake_predictor = FakeBaselinePredictor()
        with tempfile.TemporaryDirectory() as directory, patch(
            "vector_npl_research.p05.FlanT5Predictor",
            return_value=fake_predictor,
        ):
            destination = run_p05_baseline(
                RESEARCH_ROOT,
                repository_root,
                condition="few-shot-2",
                dataset_alias="p02-development",
                output_directory=Path(directory) / "run",
                seed=1234,
            )
            self.assertEqual(
                {"run.json", "predictions.jsonl", "results.jsonl", "summary.json"},
                {path.name for path in destination.iterdir()},
            )
            run = json.loads((destination / "run.json").read_text(encoding="utf-8"))
            self.assertEqual(MODEL_ID, run["modelId"])
            self.assertEqual("few-shot-2", run["parameters"]["condition"])
            self.assertEqual(
                ["p04-train-gt-01-01", "p04-train-gte-01-01"],
                run["parameters"]["demonstrationIds"],
            )
            self.assertEqual(42, run["parameters"]["maximumRenderedInputTokenCount"])
            self.assertEqual(60, run["caseCount"])
            self.assertEqual(["preflight", "load-model"], fake_predictor.calls[:2])
            self.assertEqual(60, fake_predictor.calls.count("predict"))


if __name__ == "__main__":
    unittest.main()
