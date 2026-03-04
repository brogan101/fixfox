from __future__ import annotations

from .main_window import MainWindow


class AppShell(MainWindow):
    """Incremental shell entrypoint.

    MainWindow remains the execution host while page composition is split into
    `src/ui/pages/*` modules.
    """
    def __init__(self) -> None:
        super().__init__()
