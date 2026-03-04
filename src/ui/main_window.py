from __future__ import annotations

from .main_window_impl import MainWindow as MainWindowImpl


class MainWindow(MainWindowImpl):
    """Thin orchestration surface for the Fix Fox desktop shell."""


__all__ = ["MainWindow"]
