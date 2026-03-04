from __future__ import annotations

import logging
import os
import sys
import traceback
from logging.handlers import RotatingFileHandler
from pathlib import Path
from types import TracebackType

from .paths import ensure_dirs

LOG_NAME = "fixfox"
MAX_LOG_BYTES = 1_000_000
BACKUP_COUNT = 4


def _log_file() -> Path:
    return ensure_dirs()["logs"] / "app.log"


def _crash_file() -> Path:
    return ensure_dirs()["logs"] / "crash.log"


def configure_logging() -> logging.Logger:
    logger = logging.getLogger(LOG_NAME)
    if logger.handlers:
        return logger
    logger.setLevel(logging.INFO)
    handler = RotatingFileHandler(
        _log_file(),
        maxBytes=MAX_LOG_BYTES,
        backupCount=BACKUP_COUNT,
        encoding="utf-8",
    )
    formatter = logging.Formatter("%(asctime)s %(levelname)s %(name)s %(message)s")
    handler.setFormatter(formatter)
    logger.addHandler(handler)
    return logger


def log_path() -> Path:
    return _log_file()


def logs_dir() -> Path:
    return ensure_dirs()["logs"]


def install_global_exception_handler(logger: logging.Logger | None = None) -> None:
    active_logger = logger or configure_logging()

    def handle_exception(
        exc_type: type[BaseException],
        exc_value: BaseException,
        exc_tb: TracebackType | None,
    ) -> None:
        text = "".join(traceback.format_exception(exc_type, exc_value, exc_tb))
        active_logger.exception("Unhandled exception: %s", exc_value)
        crash_path = _crash_file()
        with crash_path.open("a", encoding="utf-8") as f:
            f.write(text)
            f.write("\n")
        try:
            from PySide6.QtWidgets import QApplication, QMessageBox

            app = QApplication.instance()
            if app is not None:
                box = QMessageBox()
                box.setIcon(QMessageBox.Critical)
                box.setWindowTitle("Fix Fox - Unexpected Error")
                box.setText("An unexpected error occurred.")
                box.setInformativeText(f"Crash log saved to:\n{crash_path}")
                box.setDetailedText(text)
                copy_btn = box.addButton("Copy Error", QMessageBox.ActionRole)
                open_btn = box.addButton("Open Logs Folder", QMessageBox.ActionRole)
                close_btn = box.addButton(QMessageBox.Close)
                box.exec()
                clicked = box.clickedButton()
                if clicked is copy_btn:
                    app.clipboard().setText(text)
                elif clicked is open_btn and os.name == "nt":
                    os.startfile(str(logs_dir()))
                elif clicked is close_btn:
                    ...
        except Exception:
            ...

    sys.excepthook = handle_exception
