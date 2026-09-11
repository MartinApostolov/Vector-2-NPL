from __future__ import annotations

import hashlib
import importlib
import importlib.metadata
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .benchmark import BenchmarkCase
from .p05_config import MAX_INPUT_TOKENS, PROMPT_SHA256, P05Config
from .training_data import TrainingRecord


P05_INSTALL_COMMAND = '.\\.venv\\Scripts\\python.exe -m pip install -e ".[p05]"'


class P05DependencyError(ValueError):
    pass


class P05ModelCacheError(ValueError):
    pass


class P05PromptError(ValueError):
    pass


@dataclass(frozen=True, slots=True)
class FlanT5Dependencies:
    torch: Any
    auto_tokenizer: Any
    auto_model: Any


@dataclass(frozen=True, slots=True)
class P05Prompt:
    version: str
    template_path: Path
    template: str
    sha256: str
    condition: str
    demonstration_ids: tuple[str, ...]
    demonstrations: tuple[TrainingRecord, ...]

    def render(self, input_text: str) -> str:
        examples = "\n\n".join(
            f"Example {index}\nRequest: {record.input_text}\nOutput: {record.target_output}"
            for index, record in enumerate(self.demonstrations, start=1)
        )
        return self.template.replace("{{DEMONSTRATIONS}}", examples).replace(
            "{{INPUT}}", input_text
        )


def load_p05_prompt(
    research_root: str | Path,
    config: P05Config,
    condition: str,
    training_records: Sequence[TrainingRecord],
) -> P05Prompt:
    demonstration_ids = config.demonstration_ids(condition)
    by_id = {record.case_id: record for record in training_records}
    try:
        demonstrations = tuple(by_id[case_id] for case_id in demonstration_ids)
    except KeyError as error:
        raise P05PromptError(
            f"Frozen P05 demonstration {error.args[0]!r} is absent from validated P04 training data."
        ) from error

    path = Path(research_root) / config.prompt_template
    try:
        data = path.read_bytes()
    except OSError as error:
        raise P05PromptError(f"Unable to load the frozen P05 prompt: {error}") from error
    canonical_data = canonicalize_prompt_bytes(data)
    template = canonical_data.decode("utf-8")
    prompt_sha256 = hashlib.sha256(canonical_data).hexdigest()
    if prompt_sha256 != PROMPT_SHA256:
        raise P05PromptError(
            "Frozen P05 prompt SHA-256 does not match comparison_p05_v3.txt."
        )
    if template.count("{{DEMONSTRATIONS}}") != 1 or template.count("{{INPUT}}") != 1:
        raise P05PromptError(
            "Frozen P05 prompt must contain each required placeholder exactly once."
        )
    return P05Prompt(
        version=config.prompt_version,
        template_path=path,
        template=template,
        sha256=prompt_sha256,
        condition=condition,
        demonstration_ids=demonstration_ids,
        demonstrations=demonstrations,
    )


def canonicalize_prompt_bytes(data: bytes) -> bytes:
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise P05PromptError("Frozen P05 prompt must be valid UTF-8.") from error
    canonical = text.replace("\r\n", "\n").replace("\r", "\n").rstrip("\n") + "\n"
    return canonical.encode("utf-8")


class FlanT5Predictor:
    def __init__(
        self,
        config: P05Config,
        prompt: P05Prompt,
        *,
        random_seed: int,
        dependencies: FlanT5Dependencies | None = None,
    ) -> None:
        self._config = config
        self._prompt = prompt
        self._dependencies = dependencies or load_p05_dependencies()
        torch = self._dependencies.torch
        torch.manual_seed(random_seed)
        try:
            self._tokenizer = self._dependencies.auto_tokenizer.from_pretrained(
                config.model_id,
                revision=config.model_revision,
                local_files_only=True,
                trust_remote_code=config.trust_remote_code,
            )
        except (OSError, EnvironmentError) as error:
            raise _missing_cache_error() from error
        self._model: Any | None = None
        self._maximum_input_tokens = 0

    def load_model(self) -> None:
        if self._model is not None:
            return
        torch = self._dependencies.torch
        try:
            model = self._dependencies.auto_model.from_pretrained(
                self._config.model_id,
                revision=self._config.model_revision,
                local_files_only=True,
                trust_remote_code=self._config.trust_remote_code,
                use_safetensors=self._config.use_safetensors,
                dtype=torch.float32,
            )
        except (OSError, EnvironmentError) as error:
            raise _missing_cache_error() from error
        model.to(device="cpu", dtype=torch.float32)
        model.eval()
        self._model = model

    def preflight(
        self,
        cases: Iterable[BenchmarkCase],
        *,
        condition: str,
        dataset_alias: str,
    ) -> int:
        for case in cases:
            _, token_count = self._encode(case.input_text)
            if token_count > MAX_INPUT_TOKENS:
                raise P05PromptError(
                    f"P05 prompt preflight failed for case {case.case_id!r}: {token_count} "
                    f"tokens exceeds the {MAX_INPUT_TOKENS}-token limit "
                    f"(condition={condition!r}, dataset={dataset_alias!r}). "
                    "The run was stopped before model loading or prediction generation."
                )
        return self._maximum_input_tokens

    @property
    def model_id(self) -> str:
        return self._config.model_id

    @property
    def maximum_input_tokens(self) -> int:
        return self._maximum_input_tokens

    @property
    def model_config(self) -> Mapping[str, object]:
        model_details = getattr(self._model, "config", None) if self._model is not None else None
        return {
            "modelId": self._config.model_id,
            "revision": self._config.model_revision,
            "device": "cpu",
            "dtype": "float32",
            "trustRemoteCode": False,
            "useSafetensors": True,
            "modelClass": type(self._model).__name__ if self._model is not None else None,
            "tokenizerClass": type(self._tokenizer).__name__,
            "tokenizerMaxLength": getattr(self._tokenizer, "model_max_length", None),
            "maximumRenderedInputTokenCount": self._maximum_input_tokens,
            "actualModelConfig": _selected_model_config(model_details),
            "versions": dependency_versions(),
        }

    def predict(self, text: str) -> str:
        encoded, token_count = self._encode(text)
        if token_count > MAX_INPUT_TOKENS:
            raise P05PromptError(
                f"Rendered P05 prompt contains {token_count} input tokens; the limit is "
                f"{MAX_INPUT_TOKENS}. The run was stopped without truncation."
            )
        self.load_model()
        assert self._model is not None
        encoded = _move_to_cpu(encoded)
        with self._dependencies.torch.inference_mode():
            output = self._model.generate(
                **encoded,
                do_sample=self._config.do_sample,
                num_beams=self._config.num_beams,
                max_new_tokens=self._config.max_new_tokens,
            )
        return self._tokenizer.decode(
            output[0],
            skip_special_tokens=True,
            clean_up_tokenization_spaces=False,
        )

    def _encode(self, text: str) -> tuple[Any, int]:
        rendered = self._prompt.render(text)
        encoded = self._tokenizer(
            rendered,
            return_tensors="pt",
            truncation=False,
            add_special_tokens=True,
        )
        token_count = _input_token_count(encoded)
        self._maximum_input_tokens = max(self._maximum_input_tokens, token_count)
        return encoded, token_count


def cache_p05_model(
    config: P05Config,
    *,
    dependencies: FlanT5Dependencies | None = None,
) -> None:
    loaded = dependencies or load_p05_dependencies()
    loaded.auto_tokenizer.from_pretrained(
        config.model_id,
        revision=config.model_revision,
        trust_remote_code=config.trust_remote_code,
    )
    model = loaded.auto_model.from_pretrained(
        config.model_id,
        revision=config.model_revision,
        trust_remote_code=config.trust_remote_code,
        use_safetensors=config.use_safetensors,
        dtype=loaded.torch.float32,
    )
    model.to(device="cpu", dtype=loaded.torch.float32)
    model.eval()


def load_p05_dependencies() -> FlanT5Dependencies:
    try:
        torch = importlib.import_module("torch")
        transformers = importlib.import_module("transformers")
        importlib.import_module("sentencepiece")
        importlib.import_module("safetensors")
    except ImportError as error:
        raise P05DependencyError(
            f"P05 model support is optional. Install the frozen dependencies with "
            f"`{P05_INSTALL_COMMAND}` from research\\npl."
        ) from error
    return FlanT5Dependencies(
        torch=torch,
        auto_tokenizer=transformers.AutoTokenizer,
        auto_model=transformers.AutoModelForSeq2SeqLM,
    )


def dependency_versions() -> dict[str, str | None]:
    return {
        distribution: _distribution_version(distribution)
        for distribution in (
            "torch",
            "transformers",
            "sentencepiece",
            "safetensors",
            "tokenizers",
            "huggingface-hub",
        )
    }


def _distribution_version(distribution: str) -> str | None:
    try:
        return importlib.metadata.version(distribution)
    except importlib.metadata.PackageNotFoundError:
        return None


def _missing_cache_error() -> P05ModelCacheError:
    return P05ModelCacheError(
        "The frozen FLAN-T5-small revision is not available in the local Hugging Face "
        "cache. Run `.\\.venv\\Scripts\\python.exe -m vector_npl_research "
        "cache-p05-model` first."
    )


def _input_token_count(encoded: Any) -> int:
    try:
        input_ids = encoded["input_ids"]
    except (KeyError, TypeError) as error:
        raise P05PromptError("Tokenizer result does not contain input_ids.") from error
    shape = getattr(input_ids, "shape", None)
    if shape is not None:
        return int(shape[-1])
    try:
        return len(input_ids[0])
    except (IndexError, TypeError) as error:
        raise P05PromptError("Tokenizer returned an invalid input_ids shape.") from error


def _move_to_cpu(encoded: Any) -> Mapping[str, Any]:
    if hasattr(encoded, "to"):
        return encoded.to("cpu")
    return {
        key: value.to("cpu") if hasattr(value, "to") else value
        for key, value in encoded.items()
    }


def _selected_model_config(config: Any) -> dict[str, object]:
    if config is None:
        return {}
    selected: dict[str, object] = {}
    for output_name, attribute in (
        ("architectures", "architectures"),
        ("modelType", "model_type"),
        ("vocabSize", "vocab_size"),
        ("dModel", "d_model"),
    ):
        value = getattr(config, attribute, None)
        if isinstance(value, (str, int, float, bool, list, tuple)) or value is None:
            selected[output_name] = list(value) if isinstance(value, tuple) else value
    return selected
