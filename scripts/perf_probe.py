from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from PySide6.QtWidgets import QApplication

from src.ui.main_window import MainWindow

BUDGETS_MS = {
    "startup.splash_visible_ms": 500.0,
    "startup.main_shell_visible_ms": 2000.0,
    "startup.first_interactive_ms": 3000.0,
    "page_switch.ideal_ms": 150.0,
    "page_switch.max_ms": 300.0,
}


def _latest_perf_report(before: set[Path]) -> Path:
    candidates = set((REPO_ROOT / "logs").glob("perf_*.json"))
    new_reports = sorted(candidates - before, key=lambda path: path.stat().st_mtime, reverse=True)
    if not new_reports:
        raise RuntimeError("No new perf report was generated.")
    return new_reports[0]


def _measure_startup() -> dict[str, float]:
    env = os.environ.copy()
    env["QT_QPA_PLATFORM"] = "offscreen"
    env["FIXFOX_AUTO_EXIT_MS"] = "5000"
    before = set((REPO_ROOT / "logs").glob("perf_*.json"))
    proc = subprocess.run(
        [sys.executable, "-m", "src.app"],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        timeout=180,
        env=env,
    )
    if proc.returncode not in {0, 130}:
        raise RuntimeError(f"startup probe failed rc={proc.returncode} stderr={proc.stderr[-400:]}")
    report_path = _latest_perf_report(before)
    payload = json.loads(report_path.read_text(encoding="utf-8"))
    metrics = payload.get("metrics", {})
    return {
        "startup.splash_visible_ms": float(metrics.get("startup.splash_visible_ms", {}).get("last_ms", -1.0)),
        "startup.main_shell_visible_ms": float(metrics.get("startup.main_shell_visible_ms", {}).get("last_ms", -1.0)),
        "startup.first_interactive_ms": float(metrics.get("startup.first_interactive_ms", {}).get("last_ms", -1.0)),
        "startup.ttfp_ms": float(metrics.get("startup.ttfp_ms", {}).get("last_ms", -1.0)),
        "perf_report": str(report_path),
    }


def _measure_page_switches() -> dict[str, object]:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    app = QApplication.instance() or QApplication([])
    window = MainWindow()
    window.show()
    samples: dict[str, list[float]] = {}
    try:
        deadline = time.monotonic() + 30.0
        while getattr(window, "_startup_warmup_active", False) and time.monotonic() < deadline:
            app.processEvents()
            time.sleep(0.05)
        app.processEvents()
        for _round in range(3):
            for index, page in enumerate(window.NAV_ITEMS):
                started = time.perf_counter()
                window.nav.setCurrentRow(index)
                app.processEvents()
                time.sleep(0.03)
                app.processEvents()
                samples.setdefault(page, []).append((time.perf_counter() - started) * 1000.0)
    finally:
        window.close()
        app.processEvents()
    summary: dict[str, object] = {}
    overall_max = 0.0
    for page, page_samples in samples.items():
        avg_ms = round(sum(page_samples) / max(1, len(page_samples)), 2)
        max_ms = round(max(page_samples), 2)
        overall_max = max(overall_max, max_ms)
        summary[page] = {
            "samples_ms": [round(value, 2) for value in page_samples],
            "avg_ms": avg_ms,
            "max_ms": max_ms,
        }
    return {"pages": summary, "overall_max_ms": round(overall_max, 2)}


def main() -> int:
    startup = _measure_startup()
    page_switch = _measure_page_switches()
    result = {
        "budgets_ms": BUDGETS_MS,
        "startup": startup,
        "page_switch": page_switch,
        "budget_pass": {
            "splash": 0.0 <= float(startup["startup.splash_visible_ms"]) <= BUDGETS_MS["startup.splash_visible_ms"],
            "shell": 0.0 <= float(startup["startup.main_shell_visible_ms"]) <= BUDGETS_MS["startup.main_shell_visible_ms"],
            "interactive": 0.0 <= float(startup["startup.first_interactive_ms"]) <= BUDGETS_MS["startup.first_interactive_ms"],
            "page_switch": float(page_switch["overall_max_ms"]) <= BUDGETS_MS["page_switch.max_ms"],
        },
    }
    out_path = REPO_ROOT / "docs" / "perf_budget_report.json"
    out_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"perf_budget_report={out_path}")
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
