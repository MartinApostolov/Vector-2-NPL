from __future__ import annotations

from pathlib import Path

from .benchmark import BenchmarkCase, BenchmarkSelection, DatasetMetadata, prepare_benchmark
from .p05_config import P05_DATASETS
from .training_data import TrainingDataset, TrainingRecord, prepare_training_dataset


class P05DatasetError(ValueError):
    pass


def resolve_p05_dataset(research_root: str | Path, alias: str) -> BenchmarkSelection:
    root = Path(research_root)
    if alias == "p04-development":
        directory = root / "datasets" / "comparisons"
        dataset = prepare_training_dataset(
            directory / "comparison_training_development.jsonl",
            directory / "dataset_manifest.json",
        )
        return training_dataset_to_benchmark_selection(dataset)
    if alias == "p02-development":
        directory = root / "benchmarks" / "comparisons"
        return prepare_benchmark(
            directory / "comparison_development.jsonl",
            directory / "benchmark_manifest.json",
        )
    choices = ", ".join(P05_DATASETS)
    raise P05DatasetError(f"Unsupported P05 dataset {alias!r}; choose {choices}.")


def training_dataset_to_benchmark_selection(dataset: TrainingDataset) -> BenchmarkSelection:
    return BenchmarkSelection(
        metadata=DatasetMetadata(
            benchmark=dataset.dataset,
            dataset_file=dataset.dataset_file,
            dataset_role=dataset.dataset_role,
            dataset_sha256=dataset.dataset_sha256,
            case_count=dataset.case_count,
        ),
        cases=tuple(_training_record_to_benchmark_case(record) for record in dataset.records),
    )


def _training_record_to_benchmark_case(record: TrainingRecord) -> BenchmarkCase:
    return BenchmarkCase(
        case_id=record.case_id,
        language=record.language,
        input_text=record.input_text,
        expectation=record.expectation,
        expected=record.expected,
        reject_reason=record.reject_reason,
        tags=record.tags,
        pair=record.pair,
        notes=None,
    )
