from __future__ import annotations

import logging
import os
import unittest

from PySide6.QtCore import Qt, QtMsgType
from PySide6.QtGui import QColor, QFontMetrics, QPixmap
from PySide6.QtWidgets import QApplication, QLabel
from PySide6.QtCore import qInstallMessageHandler


def _msg_type_name(msg_type: QtMsgType) -> str:
    if msg_type == QtMsgType.QtDebugMsg:
        return "DEBUG"
    if msg_type == QtMsgType.QtInfoMsg:
        return "INFO"
    if msg_type == QtMsgType.QtWarningMsg:
        return "WARNING"
    if msg_type == QtMsgType.QtCriticalMsg:
        return "CRITICAL"
    if msg_type == QtMsgType.QtFatalMsg:
        return "FATAL"
    return "UNKNOWN"


def run_font_sanity(*, verbose: bool = True) -> tuple[bool, list[str], list[str]]:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    messages: list[str] = []
    failures: list[str] = []

    def _handler(msg_type: QtMsgType, context: object, message: str) -> None:
        del context
        line = f"[{_msg_type_name(msg_type)}] {message}"
        messages.append(line)

    prev = qInstallMessageHandler(_handler)
    ok = False
    try:
        from src.app import _load_bundled_font
        from src.core.qt_runtime import ensure_qt_runtime_env, is_font_warning, is_qss_warning
        from src.core.settings import load_settings
        from src.ui.app_qss import build_qss
        from src.ui.font_utils import font_asset_candidates
        from src.ui.theme import resolve_theme_tokens

        ensure_qt_runtime_env()
        app = QApplication.instance() or QApplication([])
        _load_bundled_font(logging.getLogger("fixfox.font_sanity"), font_asset_candidates)
        settings = load_settings().normalized()
        tokens = resolve_theme_tokens(settings.theme_palette, settings.theme_mode)
        app.setStyleSheet(build_qss(tokens, settings.theme_mode, settings.density))

        font = QApplication.font()
        metrics = QFontMetrics(font)
        for glyph in ("A", "a", "1"):
            if not metrics.inFont(glyph):
                failures.append(f"Default application font cannot render '{glyph}' (family={font.family()}).")

        probe = QLabel("Fix Fox")
        probe.ensurePolished()
        probe.adjustSize()
        canvas = QPixmap(max(96, probe.width() + 16), max(32, probe.height() + 12))
        canvas.fill(Qt.white)
        probe.render(canvas)
        image = canvas.toImage()
        white = QColor(Qt.white)
        ink_pixels = 0
        for y in range(image.height()):
            for x in range(image.width()):
                if image.pixelColor(x, y) != white:
                    ink_pixels += 1
                    if ink_pixels > 24:
                        break
            if ink_pixels > 24:
                break
        if ink_pixels <= 24:
            failures.append("Rendered 'Fix Fox' probe appears blank or glyph output is missing.")
        probe.close()

        for line in messages:
            message = line.split("] ", 1)[1] if "] " in line else line
            if is_qss_warning(message) or is_font_warning(message):
                failures.append(line)
        failures = list(dict.fromkeys(failures))
        ok = len(failures) == 0
    except Exception as exc:
        failures.append(f"Exception while checking font sanity: {exc}")
        ok = False
    finally:
        qInstallMessageHandler(prev)

    if verbose:
        print(f"font_sanity_result={'PASS' if ok else 'FAIL'}")
        for line in failures:
            print(line)
    return ok, failures, messages


class FontSanityTests(unittest.TestCase):
    def test_default_font_renders_basic_latin_and_probe_text(self) -> None:
        ok, failures, _messages = run_font_sanity(verbose=False)
        if not ok:
            self.fail("Font sanity failed:\n" + "\n".join(failures))


if __name__ == "__main__":
    raise SystemExit(unittest.main())
