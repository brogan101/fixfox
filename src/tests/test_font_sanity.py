from __future__ import annotations

import os
import unittest

from src.core.diagnostics.font_sanity import run_font_sanity


class FontSanityTests(unittest.TestCase):
    def test_default_font_renders_basic_latin_and_probe_text(self) -> None:
        os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
        result = run_font_sanity(verbose=False)
        if not result.ok:
            details = [
                f"font_family={result.font_family or '<unknown>'}",
                f"point_size={result.point_size}",
                f"weight={result.weight}",
                f"platform={result.platform_name}",
            ]
            if result.qt_warnings:
                details.append("qt_warnings=" + " | ".join(result.qt_warnings[:6]))
            self.fail("Font sanity failed:\n" + "\n".join(details + result.failures))


if __name__ == "__main__":
    raise SystemExit(unittest.main())
