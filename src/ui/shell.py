from __future__ import annotations

from typing import Any

from .main_window import MainWindow


class AppShell(MainWindow):
    """Concrete app shell host.

    This class is the stable shell entrypoint used by `src.app` while
    `MainWindow` continues to provide execution-safe behavior and worker wiring.
    """

    SHELL_ID = "fixfox_app_shell"

    def __init__(self, *, startup_phase_cb=None) -> None:
        super().__init__(startup_phase_cb=startup_phase_cb)
        self.setObjectName("AppShellWindow")
        self.setProperty("shell_id", self.SHELL_ID)

    def shell_regions(self) -> dict[str, Any]:
        return {
            "nav": getattr(self, "nav", None),
            "top_bar": getattr(self, "run_status_panel", None),
            "content": getattr(self, "pages", None),
            "right_pane": getattr(self, "side_sheet", getattr(self, "concierge", None)),
        }
