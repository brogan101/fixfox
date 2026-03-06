from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class RouteEntry:
    id: str
    title: str
    icon: str
    shortcut: str
    nav_index: int
    description: str


ROUTES: tuple[RouteEntry, ...] = (
    RouteEntry("home", "Home", "home", "Ctrl+1", 0, "Live system dashboard and next best action."),
    RouteEntry("playbooks", "Playbooks", "open_book", "Ctrl+2", 1, "Task and runbook catalog with filters."),
    RouteEntry("diagnose", "Diagnose", "diagnose", "Ctrl+3", 2, "Findings and diagnostics evidence."),
    RouteEntry("fixes", "Fixes", "wrench", "Ctrl+4", 3, "Fix actions with risk and rollback guidance."),
    RouteEntry("reports", "Reports", "reports", "Ctrl+5", 4, "Configure, preview, validate, and export packs."),
    RouteEntry("history", "History", "history", "Ctrl+6", 5, "Session history, filters, and comparisons."),
    RouteEntry("settings", "Settings", "cog", "Ctrl+7", 6, "Appearance, safety, privacy, and integrated help."),
)


def list_routes() -> tuple[RouteEntry, ...]:
    return ROUTES


def route_map() -> dict[str, RouteEntry]:
    return {entry.id: entry for entry in ROUTES}
