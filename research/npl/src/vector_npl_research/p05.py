from __future__ import annotations

from pathlib import Path

from .artifacts import write_run_artifacts
from .flan_t5 import FlanT5Predictor, cache_p05_model, load_p05_prompt
from .p05_config import P05Config, load_p05_config
from .p05_datasets import resolve_p05_dataset
from .reproducibility import validate_research_seed
from .runner import generate_predictions
from .scoring import score_predictions
from .training_data import TrainingDataset, prepare_training_dataset


def frozen_p05_config(research_root: str | Path) -> P05Config:
    return load_p05_config(Path(research_root) / "configs" / "p05_flan_t5_small.json")


def cache_frozen_p05_model(research_root: str | Path) -> None:
    cache_p05_model(frozen_p05_config(research_root))


def run_p05_baseline(
    research_root: str | Path,
    repository_root: str | Path,
    *,
    condition: str,
    dataset_alias: str,
    output_directory: str | Path,
    seed: int,
) -> Path:
    validate_research_seed(seed)
    root = Path(research_root)
    config = frozen_p05_config(root)
    demonstration_source = _prepare_demonstration_source(root)
    prompt = load_p05_prompt(root, config, condition, demonstration_source.records)
    selection = resolve_p05_dataset(root, dataset_alias)
    predictor = FlanT5Predictor(config, prompt, random_seed=seed)
    predictor.preflight(
        selection.cases,
        condition=condition,
        dataset_alias=dataset_alias,
    )
    predictor.load_model()
    predictions = generate_predictions(selection.cases, predictor, random_seed=seed)
    report = score_predictions(selection.cases, predictions)
    parameters = {
        "phase": "P05",
        "condition": condition,
        "datasetAlias": dataset_alias,
        "promptVersion": prompt.version,
        "promptSha256": prompt.sha256,
        "demonstrationIds": list(prompt.demonstration_ids),
        "demonstrationSourceDatasetFile": demonstration_source.dataset_file,
        "demonstrationSourceDatasetSha256": demonstration_source.dataset_sha256,
        "doSample": config.do_sample,
        "numBeams": config.num_beams,
        "maxNewTokens": config.max_new_tokens,
        "maximumRenderedInputTokenCount": predictor.maximum_input_tokens,
        "randomSeed": seed,
    }
    return write_run_artifacts(
        output_directory,
        selection,
        predictions,
        report,
        model_id=predictor.model_id,
        model_config=predictor.model_config,
        random_seed=seed,
        parameters=parameters,
        repository_root=repository_root,
    )


def _prepare_demonstration_source(research_root: Path) -> TrainingDataset:
    directory = research_root / "datasets" / "comparisons"
    return prepare_training_dataset(
        directory / "comparison_training.jsonl",
        directory / "dataset_manifest.json",
    )
