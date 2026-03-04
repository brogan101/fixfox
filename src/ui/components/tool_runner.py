from __future__ import annotations

import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any, Callable

from PySide6.QtCore import Qt, Signal, QTimer
from PySide6.QtGui import QFont, QTextCursor
from PySide6.QtWidgets import (
    QApplication,
    QDialog,
    QFileDialog,
    QHBoxLayout,
    QLabel,
    QMenu,
    QPushButton,
    QProgressBar,
    QTabWidget,
    QTextEdit,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from ..widgets import Card, PrimaryButton, SoftButton
from ...core.run_events import RunEvent, RunEventBus, RunEventType


class ToolRunnerWindow(QDialog):
    cancel_requested = Signal()
    rerun_requested = Signal()
    export_requested = Signal()
    output_saved = Signal(str)

    def __init__(
        self,
        tool_name: str,
        *,
        risk: str = "Safe",
        session_id: str = "",
        run_id: str = "",
        event_bus: RunEventBus | None = None,
        plain_summary: str = "",
        details_text: str = "",
        next_steps: str = "",
        mask_fn: Callable[[str], str] | None = None,
        evidence_root: str = "",
        parent: QWidget | None = None,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle(f"Tool Runner - {tool_name}")
        self.resize(900, 680)
        self._tool_name = tool_name
        self._plain_summary = plain_summary
        self._result_payload: dict[str, Any] = {}
        self._mask_fn = mask_fn
        self._start_utc = datetime.utcnow()
        self._last_status = "Running"
        self._auto_scroll = True
        self._paused = False
        self._paused_buffer: list[str] = []
        self._evidence_root = evidence_root
        self._next_step_buttons: list[QPushButton] = []
        self._run_id = str(run_id or "").strip()
        self._event_bus = event_bus
        self._event_cursor = 0

        root = QVBoxLayout(self)
        root.setContentsMargins(12, 12, 12, 12)
        root.setSpacing(10)

        header = Card(tool_name, "Running")
        row = QHBoxLayout()
        self.lbl_risk = QLabel(f"Risk: {risk}")
        self.lbl_session = QLabel(f"Session: {session_id or 'n/a'}")
        self.lbl_elapsed = QLabel("Elapsed: 0s")
        self.lbl_status = QLabel("Status: Running")
        row.addWidget(self.lbl_risk)
        row.addWidget(self.lbl_session)
        row.addWidget(self.lbl_elapsed)
        row.addWidget(self.lbl_status)
        row.addStretch(1)
        header.body_layout().addLayout(row)
        self.lbl_highlights = QLabel("Highlights: safe | findings 0 | artifacts 0")
        header.body_layout().addWidget(self.lbl_highlights)
        self.progress = QProgressBar()
        self.progress.setRange(0, 0)
        header.body_layout().addWidget(self.progress)
        root.addWidget(header)

        self.tabs = QTabWidget()
        self.txt_overview = QTextEdit()
        self.txt_overview.setReadOnly(True)
        self.txt_overview.setPlainText(plain_summary or "Plain English: running task.")

        self.txt_output = QTextEdit()
        self.txt_output.setReadOnly(True)
        self.txt_output.setFont(QFont("Consolas"))

        self.txt_details = QTextEdit()
        self.txt_details.setReadOnly(True)
        self.txt_details.setPlainText(details_text)

        self.txt_next = QTextEdit()
        self.txt_next.setReadOnly(True)
        self.txt_next.setPlainText(next_steps or "Review result, then export a support pack if needed.")
        self.next_actions = QWidget()
        next_actions_layout = QHBoxLayout(self.next_actions)
        next_actions_layout.setContentsMargins(0, 0, 0, 0)
        next_actions_layout.setSpacing(6)
        next_actions_layout.addStretch(1)

        self.tabs.addTab(self.txt_overview, "Overview")
        self.tabs.addTab(self.txt_output, "Live Output")
        self.tabs.addTab(self.txt_details, "Details")
        next_tab = QWidget()
        next_tab_layout = QVBoxLayout(next_tab)
        next_tab_layout.setContentsMargins(0, 0, 0, 0)
        next_tab_layout.setSpacing(8)
        next_tab_layout.addWidget(self.txt_next, 1)
        next_tab_layout.addWidget(self.next_actions, 0)
        self.tabs.addTab(next_tab, "Next Steps")
        root.addWidget(self.tabs, 1)

        controls = QHBoxLayout()
        self.btn_cancel = SoftButton("Cancel")
        self.btn_copy_summary = SoftButton("Copy Summary")
        self.btn_export = PrimaryButton("Export Pack")
        self.btn_more = QToolButton()
        self.btn_more.setObjectName("MoreButton")
        self.btn_more.setText("More")
        self.btn_more.setPopupMode(QToolButton.InstantPopup)
        self.btn_more_menu = QMenu(self.btn_more)
        self.btn_more_menu.addAction("Copy Ticket Summary", self.copy_ticket_summary)
        self.btn_more_menu.addAction("Copy Technical Appendix", self.copy_technical_appendix)
        self.btn_more_menu.addAction("Copy Raw Output", self.copy_raw)
        self.btn_more_menu.addAction("Save Output", self.save_output)
        self.btn_more_menu.addAction("Open Evidence Folder", self.open_evidence_folder)
        self.btn_more_menu.addAction("Pause/Resume Output", self.toggle_pause)
        self.btn_more_menu.addAction("Re-run", self.rerun_requested.emit)
        self.btn_more.setMenu(self.btn_more_menu)
        for b in (self.btn_cancel, self.btn_copy_summary, self.btn_export):
            controls.addWidget(b)
        controls.addWidget(self.btn_more)
        controls.addStretch(1)
        root.addLayout(controls)

        self.btn_cancel.clicked.connect(self.cancel_requested.emit)
        self.btn_copy_summary.clicked.connect(self.copy_summary)
        self.btn_export.clicked.connect(self.export_requested.emit)

        self._tick_timer = QTimer(self)
        self._tick_timer.timeout.connect(self._tick_elapsed)
        self._tick_timer.start(1000)
        self._event_timer = QTimer(self)
        self._event_timer.timeout.connect(self._drain_event_bus)
        self._event_timer.start(80)
        self.attach_event_bus(event_bus, self._run_id)

    def _tick_elapsed(self) -> None:
        elapsed = int((datetime.utcnow() - self._start_utc).total_seconds())
        self.lbl_elapsed.setText(f"Elapsed: {elapsed}s")

    def attach_event_bus(self, event_bus: RunEventBus | None, run_id: str) -> None:
        self._event_bus = event_bus
        self._run_id = str(run_id or "").strip()
        self._event_cursor = 0
        self._drain_event_bus()

    def _drain_event_bus(self) -> None:
        if self._event_bus is None or not self._run_id:
            return
        events, cursor = self._event_bus.events_since(self._run_id, self._event_cursor)
        if not events:
            return
        self._event_cursor = cursor
        for event in events:
            self._apply_bus_event(event)

    def _apply_bus_event(self, event: RunEvent) -> None:
        kind = str(event.event_type).strip().upper()
        message = str(event.message or "").strip()
        if kind == RunEventType.START:
            self.lbl_status.setText("Status: Running")
            if message:
                self._append(f"[start] {message}")
            return
        if kind == RunEventType.PROGRESS:
            pct = int(event.progress if event.progress is not None else 0)
            self.progress.setRange(0, 100)
            self.progress.setValue(max(0, min(100, pct)))
            self.lbl_status.setText(f"Status: Running ({pct}%)")
            if message:
                self._append(f"[progress] {pct}% {message}")
            return
        if kind == RunEventType.STDOUT:
            if message:
                self._append(message)
            return
        if kind == RunEventType.STDERR:
            if message:
                self._append(message if message.startswith("[stderr]") else f"[stderr] {message}")
            return
        if kind == RunEventType.ARTIFACT:
            if message:
                self._append(f"[artifact] {message}")
            return
        if kind == RunEventType.WARNING:
            if message:
                self._append(f"[warn] {message}")
            return
        if kind == RunEventType.ERROR:
            if message:
                self._append(f"[error] {message}")
            return
        if kind == RunEventType.END:
            if self._last_status == "Running":
                data = event.data or {}
                code = int(data.get("code", 0)) if isinstance(data, dict) else 0
                if code == 130:
                    self._last_status = "Cancelled"
                elif code == 0:
                    self._last_status = "Completed"
                else:
                    self._last_status = "Failed"
                self.lbl_status.setText(f"Status: {self._last_status}")
            if message:
                self._append(f"[end] {message}")

    def _append(self, line: str) -> None:
        if not line:
            return
        if self._paused:
            self._paused_buffer.append(line)
            return
        self.txt_output.append(line)
        if self._auto_scroll:
            cursor = self.txt_output.textCursor()
            cursor.movePosition(QTextCursor.End)
            self.txt_output.setTextCursor(cursor)

    def toggle_pause(self) -> None:
        self._paused = not self._paused
        if self._paused:
            self._append("[output] Paused.")
        else:
            self._append("[output] Resumed.")
        if not self._paused and self._paused_buffer:
            chunk = self._paused_buffer
            self._paused_buffer = []
            for line in chunk:
                self._append(line)

    def on_progress(self, pct: int, text: str) -> None:
        self.lbl_status.setText(f"Status: Running ({pct}%)")
        self.progress.setRange(0, 100)
        self.progress.setValue(max(0, min(100, pct)))
        if text:
            self._append(f"[progress] {pct}% {text}")

    def on_log_line(self, line: str) -> None:
        self._append(line)

    def on_partial(self, payload: Any) -> None:
        self.lbl_status.setText("Status: Partial update")
        if isinstance(payload, dict):
            self._append("[partial] " + json.dumps(payload, ensure_ascii=False)[:1400])

    def on_result(self, payload: dict[str, Any]) -> None:
        self._drain_event_bus()
        self._result_payload = payload or {}
        result_root = str(self._result_payload.get("evidence_root", "")).strip() if isinstance(self._result_payload, dict) else ""
        if result_root:
            self._evidence_root = result_root
        code = int(self._result_payload.get("code", 0)) if isinstance(self._result_payload, dict) else 0
        cancelled = bool(self._result_payload.get("cancelled")) if isinstance(self._result_payload, dict) else False
        failures = 0
        if isinstance(self._result_payload, dict) and isinstance(self._result_payload.get("steps"), list):
            for step in self._result_payload.get("steps", []):
                if not isinstance(step, dict):
                    continue
                result = step.get("result", {})
                if not isinstance(result, dict):
                    continue
                if int(result.get("code", 0)) != 0:
                    failures += 1
        if cancelled or code == 130:
            self._last_status = "Cancelled"
        elif failures > 0:
            self._last_status = "Partial"
        elif code != 0:
            self._last_status = "Failed"
        else:
            self._last_status = "Completed"
        self.lbl_status.setText(f"Status: {self._last_status}")
        self.progress.setRange(0, 100)
        self.progress.setValue(100)
        self.btn_cancel.setEnabled(False)
        reason = str(self._result_payload.get("user_message", "")).strip()
        if not reason and self._last_status in {"Failed", "Partial"}:
            reason = str(self._result_payload.get("stderr", "")).strip() or "One or more steps returned a non-zero code."
        if reason:
            self._append(f"[reason] {reason}")
        findings_count = int(self._result_payload.get("findings_count", 0)) if str(self._result_payload.get("findings_count", "")).isdigit() else failures
        artifacts = len(self._result_payload.get("output_files", [])) + len(self._result_payload.get("evidence_files", []))
        reboot = bool(self._result_payload.get("reboot_likely"))
        risk = str(self.lbl_risk.text().replace("Risk:", "").strip() or "Safe").lower()
        reboot_chip = "reboot likely" if reboot else "no reboot"
        self.lbl_highlights.setText(
            f"Highlights: {risk} | {reboot_chip} | findings {findings_count} | artifacts {artifacts}"
        )
        next_steps = self._extract_next_steps()
        self.txt_overview.setPlainText(self._build_overview(findings_count, artifacts, next_steps))
        self.txt_next.setPlainText("\n".join([f"- {row}" for row in next_steps]))
        self._rebuild_next_action_buttons(next_steps)
        self._append(f"[done] {self._last_status}.")
        self._drain_event_bus()

    def on_error(self, message: str) -> None:
        self._drain_event_bus()
        self._last_status = "Failed"
        self.lbl_status.setText("Status: Failed")
        self.btn_cancel.setEnabled(False)
        reason = self._parse_reason(message)
        self._append("[error] " + reason)
        steps = self._failure_next_steps(message)
        self.txt_overview.setPlainText(
            "\n".join(
                [
                    f"What ran:\n- {self._tool_name}",
                    f"What was found:\n- Run failed before completion. Reason: {reason}",
                    "What changed:\n- Operation stopped; partial artifacts may still be available.",
                    "Next steps:",
                    *[f"- {row}" for row in steps],
                    "",
                    self._context_block(artifacts=0),
                ]
            )
        )
        self._rebuild_next_action_buttons(steps)
        self.txt_next.setPlainText("\n".join([f"- {row}" for row in steps]))
        self._drain_event_bus()

    def on_cancelled(self) -> None:
        self._drain_event_bus()
        self._last_status = "Cancelled"
        self.lbl_status.setText("Status: Cancelled")
        self.btn_cancel.setEnabled(False)
        self._append("[cancelled] Cancellation requested.")
        self._drain_event_bus()

    def copy_summary(self) -> None:
        summary = self._plain_summary or f"{self._tool_name}: {self._last_status}"
        if isinstance(self._result_payload, dict) and self._result_payload.get("summary_text"):
            summary = str(self._result_payload.get("summary_text"))
        if self._mask_fn is not None:
            summary = self._mask_fn(summary)
        QApplication.clipboard().setText(summary)

    def copy_ticket_summary(self) -> None:
        lines = [
            f"Tool: {self._tool_name}",
            f"Status: {self._last_status}",
            f"Elapsed: {self.lbl_elapsed.text().replace('Elapsed:', '').strip()}",
        ]
        if isinstance(self._result_payload, dict):
            if self._result_payload.get("summary_text"):
                lines.append("")
                lines.append(str(self._result_payload.get("summary_text")))
            detail = str(self._result_payload.get("technical_message", "")).strip()
            if detail:
                lines.append("")
                lines.append("Technical:")
                lines.append(detail)
        text = "\n".join(lines)
        if self._mask_fn is not None:
            text = self._mask_fn(text)
        QApplication.clipboard().setText(text)

    def copy_technical_appendix(self) -> None:
        detail = self.txt_details.toPlainText().strip()
        if not detail and isinstance(self._result_payload, dict):
            detail = str(self._result_payload.get("technical_message", "")).strip()
        if not detail:
            detail = "No technical appendix is available for this run."
        if self._mask_fn is not None:
            detail = self._mask_fn(detail)
        QApplication.clipboard().setText(detail)

    def copy_raw(self) -> None:
        text = self.txt_output.toPlainText()
        if self._mask_fn is not None:
            text = self._mask_fn(text)
        QApplication.clipboard().setText(text)

    def save_output(self) -> None:
        suggested = f"{self._tool_name.replace(' ', '_').lower()}_output.txt"
        if self._evidence_root:
            suggested = str(Path(self._evidence_root) / suggested)
        path, _ = QFileDialog.getSaveFileName(self, "Save Tool Runner Output", suggested, "Text (*.txt)")
        if not path:
            return
        text = self.txt_output.toPlainText()
        if self._mask_fn is not None:
            text = self._mask_fn(text)
        Path(path).write_text(text, encoding="utf-8")
        self.output_saved.emit(path)

    def open_evidence_folder(self) -> None:
        root = ""
        if isinstance(self._result_payload, dict):
            root = str(self._result_payload.get("evidence_root", ""))
            if not root:
                files = self._result_payload.get("evidence_files", [])
                if files:
                    root = str(Path(str(files[0])).parent)
        if not root:
            root = self._evidence_root
        if not root:
            return
        p = Path(root)
        if not p.exists():
            return
        if os.name == "nt":
            os.startfile(str(p))

    def _extract_next_steps(self) -> list[str]:
        if not isinstance(self._result_payload, dict):
            return self._dedupe_steps([
                "Re-run the tool.",
                "Try a related safe diagnostic.",
                "Export a partial support pack.",
            ])
        direct = self._result_payload.get("next_steps_list", [])
        if isinstance(direct, list):
            steps = [str(row).strip() for row in direct if str(row).strip()]
            if steps:
                return self._merge_with_baseline_steps(steps)
        suggested = self._result_payload.get("suggested_next_steps", [])
        if isinstance(suggested, list):
            steps = [str(row).strip() for row in suggested if str(row).strip()]
            if steps:
                return self._merge_with_baseline_steps(steps)
        text = str(self._result_payload.get("next_steps", "")).strip()
        if text:
            return self._merge_with_baseline_steps([line.strip("- ").strip() for line in text.splitlines() if line.strip()])
        return self._merge_with_baseline_steps([
            "Re-run the tool.",
            "Try a related safe diagnostic.",
            "Export a partial support pack.",
        ])

    def _parse_reason(self, message: str) -> str:
        text = (message or "").strip()
        if not text:
            return "Unknown error."
        for raw_line in text.splitlines():
            line = raw_line.strip()
            if line.lower().startswith("why:"):
                reason = line.split(":", 1)[1].strip()
                if reason:
                    return reason
            if line.lower().startswith("reason:"):
                reason = line.split(":", 1)[1].strip()
                if reason:
                    return reason
        return text.splitlines()[0].strip()

    def _failure_next_steps(self, message: str) -> list[str]:
        parsed: list[str] = []
        for raw_line in (message or "").splitlines():
            line = raw_line.strip()
            if line.lower().startswith("try next:"):
                step = line.split(":", 1)[1].strip()
                if step:
                    parsed.append(step)
        baseline = [
            "Re-run the same action.",
            "Run a related safe diagnostic first.",
            "Export a partial support pack.",
        ]
        return self._dedupe_steps(parsed + baseline)

    def _merge_with_baseline_steps(self, steps: list[str]) -> list[str]:
        if self._last_status in {"Failed", "Partial", "Cancelled"}:
            baseline = [
                "Re-run the same action.",
                "Run a related safe diagnostic first.",
                "Export a partial support pack.",
            ]
        else:
            baseline = [
                "Export a support pack if you need to share this result.",
            ]
        return self._dedupe_steps(steps + baseline)

    def _dedupe_steps(self, steps: list[str]) -> list[str]:
        out: list[str] = []
        seen: set[str] = set()
        for step in steps:
            text = str(step).strip()
            if not text:
                continue
            key = text.lower()
            if key in seen:
                continue
            seen.add(key)
            out.append(text)
            if len(out) >= 4:
                break
        return out

    def _build_overview(self, findings_count: int, artifacts: int, next_steps: list[str]) -> str:
        summary = ""
        if isinstance(self._result_payload, dict):
            summary = str(self._result_payload.get("summary_text", "")).strip()
        what_found = summary.splitlines()[0] if summary else f"Status {self._last_status} with {findings_count} finding(s)."
        changed_line = "No direct system changes detected."
        if isinstance(self._result_payload, dict):
            code = int(self._result_payload.get("code", 0))
            if code == 0:
                changed_line = "Action completed successfully; review generated artifacts."
            elif code == 130:
                changed_line = "Action was cancelled before full completion."
            else:
                changed_line = "Action encountered issues; review details tab for technical context."
        lines = [
            f"What ran:\n- {self._tool_name}",
            f"What was found:\n- {what_found}",
            f"What changed:\n- {changed_line}",
            "Next steps:",
        ]
        for row in next_steps[:4]:
            lines.append(f"- {row}")
        lines.append("")
        lines.append(self._context_block(artifacts))
        return "\n".join(lines)

    def _context_block(self, artifacts: int) -> str:
        name = self._tool_name.lower()
        if any(token in name for token in ("network", "wifi", "proxy", "dns")):
            return f"Network context: review adapter/IP and DNS/proxy outputs. Artifacts: {artifacts}."
        if any(token in name for token in ("update", "windows update", "reboot")):
            return f"Updates context: check reboot pending and update service status. Artifacts: {artifacts}."
        if any(token in name for token in ("printer", "spooler")):
            return f"Printer context: verify spooler status and queue health. Artifacts: {artifacts}."
        if any(token in name for token in ("storage", "disk", "space")):
            return f"Storage context: confirm free-space deltas and largest offenders. Artifacts: {artifacts}."
        if any(token in name for token in ("integrity", "sfc", "dism", "reliability")):
            return f"Integrity context: review scan timing, reboot warnings, and follow-up checks. Artifacts: {artifacts}."
        return f"Execution context: review overview + details for deterministic follow-up. Artifacts: {artifacts}."

    def _rebuild_next_action_buttons(self, steps: list[str]) -> None:
        layout = self.next_actions.layout()
        if layout is None:
            return
        for btn in self._next_step_buttons:
            btn.deleteLater()
        self._next_step_buttons = []
        while layout.count():
            item = layout.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.deleteLater()
        for step in steps[:3]:
            btn = SoftButton(step[:46] + ("..." if len(step) > 46 else ""))
            btn.clicked.connect(lambda _checked=False, text=step: self._run_next_step_action(text))
            layout.addWidget(btn)
            self._next_step_buttons.append(btn)
        layout.addStretch(1)

    def _run_next_step_action(self, text: str) -> None:
        lower = (text or "").strip().lower()
        if "export" in lower:
            self.export_requested.emit()
            return
        if "copy ticket summary" in lower:
            self.copy_ticket_summary()
            return
        QApplication.clipboard().setText(text)

    @property
    def run_id(self) -> str:
        return self._run_id
