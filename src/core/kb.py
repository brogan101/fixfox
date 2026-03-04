from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class KBCard:
    id: str
    title: str
    why_it_matters: str
    next_steps: str
    when_to_escalate: str


KB_CARDS: tuple[KBCard, ...] = (
    KBCard(
        "kb_disk_space",
        "Low Disk Space",
        "Low free space can slow updates and app performance.",
        "Clear temporary files and review large downloads first.",
        "Escalate when free space stays under 10 GB after cleanup.",
    ),
    KBCard(
        "kb_proxy_hosts",
        "Proxy or Hosts Overrides",
        "Unexpected proxy or hosts rules can redirect traffic and break access.",
        "Review proxy settings and hosts entries before making changes.",
        "Escalate when settings re-apply after reboot or policy refresh.",
    ),
    KBCard(
        "kb_memory",
        "Memory Pressure",
        "High memory usage causes sluggish switching and background app failures.",
        "Close heavy apps and check startup entries.",
        "Escalate when memory usage remains above 90% at idle.",
    ),
)
