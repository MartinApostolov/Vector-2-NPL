from __future__ import annotations

import json
import platform
import subprocess
import sys
from collections.abc import Mapping
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from .benchmark import BenchmarkSelection
from .predictions import Prediction
from .reproducibility import validate_research_seed
from .scoring import ScoreReport


@dataclass(frozen=True, slots=True)
class GitState:
    commit: str
    dirty: bool


def capture_git_state(repository_root: str | Path) -> GitState:
    root = Path(repository_root).resolve()
    git_prefix = ["git", "-c", f"safe.directory={root.as_posix()}"]
    commit = subprocess.run(
        [*git_prefix, "rev-parse", "HEAD"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    ).stdout.strip()
    status = subprocess.run(
        [*git_prefix, "status", "--porcelain"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    ).stdout
    return GitState(commit=commit, dirty=bool(status.strip()))


def write_run_artifacts(
    output_directory: str | Path,
    selection: BenchmarkSelection,
    predictions: tuple[Prediction, ...],
    report: ScoreReport,
    *,
    model_id: str,
    model_config: Mapping[str, object],
    random_seed: int,
    parameters: Mapping[str, object],
    repository_root: str | Path,
    run_id: str | None = None,
    created_at_utc: str | None = None,
    git_state: GitState | None = None,
) -> Path:
    validate_research_seed(random_seed)
    if not model_id.strip():
        raise ValueError("model_id must be nonblank.")

    created = created_at_utc or datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    effective_run_id = run_id or f"{created.replace(':', '').replace('-', '')}-{model_id}"
    state = git_state or capture_git_state(repository_root)

    destination = Path(output_directory)
    destination.mkdir(parents=True, exist_ok=True)
    artifact_names = ("run.json", "predictions.jsonl", "results.jsonl", "summary.json")
    existing = [name for name in artifact_names if (destination / name).exists()]
    if existing:
        raise FileExistsError(f"Run artifact files already exist: {', '.join(existing)}")

    metadata = selection.metadata
    run_data = {
        "schemaVersion": 1,
        "runId": effective_run_id,
        "createdAtUtc": created,
        "gitCommit": state.commit,
        "gitDirty": state.dirty,
        "pythonVersion": platform.python_version(),
        "platform": platform.platform(),
        "benchmark": metadata.benchmark,
        "datasetFile": metadata.dataset_file,
        "datasetRole": metadata.dataset_role,
        "datasetSha256": metadata.dataset_sha256,
        "caseCount": metadata.case_count,
        "modelId": model_id,
        "modelConfig": dict(model_config),
        "randomSeed": random_seed,
        "parameters": dict(parameters),
    }

    _write_json(destination / "run.json", run_data)
    _write_jsonl(
        destination / "predictions.jsonl",
        (
            {"caseId": prediction.case_id, "rawOutput": prediction.raw_output}
            for prediction in sorted(predictions, key=lambda item: item.case_id)
        ),
    )
    _write_jsonl(
        destination / "results.jsonl",
        (result.to_data() for result in report.results),
    )
    _write_json(destination / "summary.json", report.summary)
    return destination


def default_run_directory(research_root: str | Path, label: str) -> Path:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    return Path(research_root) / "runs" / f"{timestamp}-{label}"


def _write_json(path: Path, value: Any) -> None:
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True, allow_nan=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _write_jsonl(path: Path, values: Any) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        for value in values:
            stream.write(
                json.dumps(value, ensure_ascii=False, sort_keys=True, allow_nan=False, separators=(",", ":"))
            )
            stream.write("\n")
