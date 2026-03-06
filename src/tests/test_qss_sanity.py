from __future__ import annotations

import os
import unittest

from src.core.diagnostics.qss_sanity import run_qss_sanity


class QssSanityTests(unittest.TestCase):
    def test_qss_has_no_parse_or_unknown_property_warnings(self) -> None:
        os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
        result = run_qss_sanity(verbose=False)
        if not result.ok:
            self.fail("QSS sanity failed:\n" + "\n".join(result.failures))


if __name__ == "__main__":
    raise SystemExit(unittest.main())
