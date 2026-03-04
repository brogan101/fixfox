from __future__ import annotations

import os
import re
from dataclasses import dataclass
from typing import Iterable


USER_PATH_RE = re.compile(r"(?i)C:\\Users\\([^\\\s]+)")
HOSTNAME_RE = re.compile(r"\b(?:DESKTOP|LAPTOP)-[A-Z0-9]{4,}\b", re.IGNORECASE)
IP_RE = re.compile(r"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b")


@dataclass(frozen=True)
class MaskingOptions:
    enabled: bool = True
    mask_ip: bool = False
    extra_tokens: tuple[str, ...] = ()


def _hostname_tokens() -> set[str]:
    tokens: set[str] = set()
    for key in ("COMPUTERNAME", "HOSTNAME"):
        value = os.environ.get(key)
        if value:
            tokens.add(value)
    return tokens


def mask_text(text: str, options: MaskingOptions) -> str:
    if not options.enabled or not text:
        return text

    masked = text
    masked = USER_PATH_RE.sub(r"C:\\Users\\<user>", masked)
    masked = HOSTNAME_RE.sub("<pc-name>", masked)

    for token in _hostname_tokens().union(set(options.extra_tokens)):
        if token:
            masked = masked.replace(token, "<redacted>")

    if options.mask_ip:
        masked = IP_RE.sub("<ip>", masked)
    return masked


def mask_values(values: Iterable[str], options: MaskingOptions) -> list[str]:
    return [mask_text(value, options) for value in values]


def redaction_preview(sample: str, options: MaskingOptions) -> dict[str, str]:
    return {"before": sample, "after": mask_text(sample, options)}
