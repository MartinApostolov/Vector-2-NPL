from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path
from typing import Any

from .artifacts import write_run_artifacts
from .benchmark import BenchmarkError, SealedGateBlockedError, prepare_benchmark
from .fixture import run_synthetic_fixture
from .model_output import parse_model_output
from .p05 import cache_frozen_p05_model, run_p05_baseline
from .p05_config import CANONICAL_SEED, P05_CONDITIONS, P05_DATASETS
from .predictions import PredictionError, load_prediction_file
from .reproducibility import validate_research_seed
from .scoring import score_predictions
from .semantic import semantic_to_data


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="vector-npl-research",
        description="Model-independent research harness for the Vector NPL semantic proof.",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate = subparsers.add_parser("validate-output", help="Validate raw model text.")
    validate_source = validate.add_mutually_exclusive_group(required=True)
    validate_source.add_argument("--text", help="Raw model output text.")
    validate_source.add_argument("--file", type=Path, help="File containing raw model output text.")

    score = subparsers.add_parser("score", help="Score complete external prediction JSONL.")
    score.add_argument("--benchmark", type=Path, required=True)
    score.add_argument("--manifest", type=Path, required=True)
    score.add_argument("--predictions", type=Path, required=True)
    score.add_argument("--output-dir", type=Path, required=True)
    score.add_argument("--model-id", required=True)
    score.add_argument("--model-config", default="{}", help="JSON object; defaults to {}.")
    score.add_argument("--parameters", default="{}", help="JSON object; defaults to {}.")
    score.add_argument("--seed", type=int, required=True)
    score.add_argument(
        "--allow-sealed-gate",
        action="store_true",
        help="Explicitly unlock a sealed-role dataset for the future P07 gate only.",
    )

    fixture = subparsers.add_parser(
        "run-fixture",
        help="Run the fixed predictor against a small synthetic development benchmark.",
    )
    fixture.add_argument("--output-dir", type=Path)
    fixture.add_argument("--seed", type=int, default=1234)

    subparsers.add_parser(
        "cache-p05-model",
        help="Download and cache the exact frozen P05 FLAN-T5-small revision.",
    )

    p05 = subparsers.add_parser(
        "run-p05-baseline",
        help="Run the frozen cache-only P05 FLAN-T5-small baseline.",
    )
    p05.add_argument("--condition", choices=P05_CONDITIONS, required=True)
    p05.add_argument("--dataset", choices=P05_DATASETS, required=True)
    p05.add_argument("--output-dir", type=Path, required=True)
    p05.add_argument("--seed", type=int, default=CANONICAL_SEED)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    arguments = parser.parse_args(argv)
    research_root = Path(__file__).resolve().parents[2]
    repository_root = research_root.parents[1]

    try:
        if arguments.command == "validate-output":
            raw = arguments.text
            if arguments.file is not None:
                raw = arguments.file.read_text(encoding="utf-8")
            parsed = parse_model_output(raw)
            output = {
                "classification": (
                    "VALID_SEMANTIC"
                    if parsed.outcome == "semantic"
                    else "VALID_REJECT"
                    if parsed.outcome == "reject"
                    else "INVALID_MODEL_OUTPUT"
                ),
                "semantic": semantic_to_data(parsed.semantic) if parsed.semantic else None,
                "error": (
                    {
                        "code": parsed.error_code,
                        "path": parsed.error_path,
                        "message": parsed.error_message,
                    }
                    if not parsed.valid
                    else None
                ),
            }
            print(json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True))
            return 0 if parsed.valid else 2

        if arguments.command == "score":
            validate_research_seed(arguments.seed)
            selection = prepare_benchmark(
                arguments.benchmark,
                arguments.manifest,
                allow_sealed_gate=arguments.allow_sealed_gate,
            )
            predictions = load_prediction_file(
                arguments.predictions,
                frozenset(case.case_id for case in selection.cases),
            )
            report = score_predictions(selection.cases, predictions)
            model_config = _json_object(arguments.model_config, "--model-config")
            parameters = _json_object(arguments.parameters, "--parameters")
            destination = write_run_artifacts(
                arguments.output_dir,
                selection,
                predictions,
                report,
                model_id=arguments.model_id,
                model_config=model_config,
                random_seed=arguments.seed,
                parameters=parameters,
                repository_root=repository_root,
            )
            print(f"Wrote run artifacts to {destination}")
            return 0

        if arguments.command == "run-fixture":
            destination = run_synthetic_fixture(
                research_root,
                repository_root,
                output_directory=arguments.output_dir,
                seed=arguments.seed,
            )
            print(f"Synthetic fixture passed; artifacts written to {destination}")
            return 0

        if arguments.command == "cache-p05-model":
            cache_frozen_p05_model(research_root)
            print("Cached the frozen P05 FLAN-T5-small model and tokenizer revision.")
            return 0

        if arguments.command == "run-p05-baseline":
            destination = run_p05_baseline(
                research_root,
                repository_root,
                condition=arguments.condition,
                dataset_alias=arguments.dataset,
                output_directory=arguments.output_dir,
                seed=arguments.seed,
            )
            print(f"P05 baseline artifacts written to {destination}")
            return 0
    except (BenchmarkError, PredictionError, ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    parser.error("Unknown command.")
    return 2


def _json_object(text: str, option_name: str) -> dict[str, Any]:
    value = json.loads(text)
    if not isinstance(value, dict):
        raise ValueError(f"{option_name} must contain a JSON object.")
    return value
