from __future__ import annotations

import unittest

from scripts.verify_requirements import run_verification


class RequirementsGateTests(unittest.TestCase):
    def test_requirements_gate(self) -> None:
        outcome = run_verification(verbose=False)
        if not outcome.passed:
            self.fail(outcome.render_console())


if __name__ == "__main__":
    raise SystemExit(unittest.main())
