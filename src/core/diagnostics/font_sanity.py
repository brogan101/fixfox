from __future__ import annotations

import logging
import os
import platform
import tempfile
from dataclasses import dataclass
from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QFont, QFontMetrics, QPixmap
from PySide6.QtWidgets import QApplication, QLabel

from ..qt_runtime import is_fatal_qt_warning, is_font_warning, is_qss_warning
from ..settings import load_settings
from ...ui.runtime_bootstrap import apply_runtime_ui_bootstrap
from .qt_warnings import install_qt_message_handler, read_qt_warnings


@dataclass
class FontSanityResult:
    ok: bool
    failures: list[str]
    qt_warnings: list[str]
    font_family: str
    point_size: float
    weight: int
    platform_name: str
    report_path: str = ""


def probe_font_render(font: QFont, *, sample_text: str = "FixFox runtime readable text") -> list[str]:
    failures: list[str] = []
    metrics = QFontMetrics(font)
    for glyph in ("A", "a", "1", "-", "FixFox"):
        if not metrics.inFont(glyph[0]):
            failures.append(f"Default application font cannot render '{glyph[0]}'.")

    probe = QLabel(sample_text)
    probe.setObjectName("FontSanityProbe")
    probe.setFont(font)
    probe.ensurePolished()
    probe.adjustSize()
    canvas = QPixmap(max(220, probe.width() + 24), max(56, probe.height() + 18))
    canvas.fill(Qt.transparent)
    probe.render(canvas)
    image = canvas.toImage()
    alpha_pixels = 0
    unique_rgba: set[tuple[int, int, int, int]] = set()
    for y in range(image.height()):
        for x in range(image.width()):
            color = image.pixelColor(x, y)
            rgba = (color.red(), color.green(), color.blue(), color.alpha())
            if rgba[3] > 0:
                alpha_pixels += 1
                unique_rgba.add(rgba)
                if alpha_pixels > 32 and len(unique_rgba) > 1:
                    break
        if alpha_pixels > 32 and len(unique_rgba) > 1:
            break
    probe.close()
    if alpha_pixels <= 32 or len(unique_rgba) <= 1:
        failures.append(f"Rendered QLabel({sample_text!r}) probe appears blank or non-varying; tofu risk remains.")
    return failures


def run_font_sanity(
    *,
    report_path: str | Path | None = None,
    warning_log_path: str | Path | None = None,
    verbose: bool = True,
) -> FontSanityResult:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    warnings_path = Path(warning_log_path) if warning_log_path else Path(tempfile.gettempdir()) / f"fixfox_font_sanity_{os.getpid()}.log"
    warnings_path.parent.mkdir(parents=True, exist_ok=True)
    warnings_path.write_text("", encoding="utf-8")
    cleanup = install_qt_message_handler(str(warnings_path))
    failures: list[str] = []
    font_family = ""
    point_size = 0.0
    weight = 0
    platform_name = platform.platform()
    try:
        app = QApplication.instance() or QApplication([])
        settings = load_settings().normalized()
        apply_runtime_ui_bootstrap(app, logger=logging.getLogger("fixfox.font_sanity"), settings=settings)

        font = app.font()
        font_family = font.family()
        point_size = float(font.pointSizeF() if font.pointSizeF() > 0 else font.pointSize())
        weight = int(font.weight())
        failures.extend(probe_font_render(font))
    except Exception as exc:
        failures.append(f"Exception while checking font sanity: {exc}")
    finally:
        cleanup()

    qt_warnings = read_qt_warnings(warnings_path)
    for line in qt_warnings:
        message = line.split("] ", 1)[1] if "] " in line else line
        if is_qss_warning(message) or is_font_warning(message) or is_fatal_qt_warning(message):
            failures.append(line)
    failures = list(dict.fromkeys(failures))
    ok = len(failures) == 0

    resolved_report = Path(report_path) if report_path else None
    if resolved_report is not None:
        resolved_report.parent.mkdir(parents=True, exist_ok=True)
        lines = [
            "FixFox Font Sanity Check",
            f"result={'OK' if ok else 'FAIL'}",
            f"font_family={font_family or '<unknown>'}",
            f"point_size={point_size}",
            f"weight={weight}",
            f"platform={platform_name}",
            "",
            "Failures:",
        ]
        if failures:
            lines.extend(f"- {line}" for line in failures)
        else:
            lines.append("- none")
        lines += ["", "Qt Warnings:"]
        if qt_warnings:
            lines.extend(f"- {line}" for line in qt_warnings)
        else:
            lines.append("- none")
        resolved_report.write_text("\n".join(lines) + "\n", encoding="utf-8")

    result = FontSanityResult(
        ok=ok,
        failures=failures,
        qt_warnings=qt_warnings,
        font_family=font_family,
        point_size=point_size,
        weight=weight,
        platform_name=platform_name,
        report_path=str(resolved_report) if resolved_report else "",
    )
    if verbose:
        print(f"font_sanity_result={'PASS' if result.ok else 'FAIL'}")
        if not result.ok:
            print(f"font_family={result.font_family or '<unknown>'}")
            print(f"point_size={result.point_size}")
            print(f"weight={result.weight}")
            print(f"platform={result.platform_name}")
            for line in result.failures:
                print(line)
            if result.qt_warnings:
                print("qt_warnings:")
                for line in result.qt_warnings:
                    print(line)
    return result
