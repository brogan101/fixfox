from __future__ import annotations

from dataclasses import dataclass

from .settings import AppSettings


@dataclass(frozen=True)
class SafetyPolicy:
    safe_only: bool
    show_admin: bool
    show_advanced: bool
    diagnostic_mode: bool


def policy_from_settings(settings: AppSettings) -> SafetyPolicy:
    return SafetyPolicy(
        safe_only=settings.safe_only_mode,
        show_admin=settings.show_admin_tools,
        show_advanced=settings.show_advanced_tools,
        diagnostic_mode=settings.diagnostic_mode,
    )


def can_show_action(risk: str, policy: SafetyPolicy) -> bool:
    if risk == "Safe":
        return True
    if risk == "Admin":
        return policy.show_admin and not policy.safe_only
    if risk == "Advanced":
        return policy.show_advanced and not policy.safe_only
    return True


def can_execute_fix(risk: str, policy: SafetyPolicy) -> bool:
    if policy.diagnostic_mode:
        return False
    if risk == "Safe":
        return True
    if policy.safe_only:
        return False
    if risk == "Admin":
        return policy.show_admin
    if risk == "Advanced":
        return policy.show_advanced
    return True
