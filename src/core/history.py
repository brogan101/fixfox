"""Compatibility wrappers around session persistence APIs."""

from __future__ import annotations

from .sessions import SessionMeta, add_or_update_meta as add_session, load_index, new_session_id, save_index

__all__ = ["SessionMeta", "add_session", "load_index", "new_session_id", "save_index"]
