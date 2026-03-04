# Changelog

## 0.9.0-beta1 - 2026-03-03

### UI/UX
- Standardized shell and page structure across all 7 nav surfaces.
- Context bar made session-aware (compact hint when no active session).
- Right panel converted to true detail/action context pane.
- Reports upgraded to guided 3-step export flow.
- Settings upgraded with search, reset defaults, export JSON.

### Tools and Runbooks
- Wi-Fi wizard deepened with DNS/proxy/hosts/WLAN + network bundle + safe DNS flush.
- Storage radar upgraded with bounded scan, ranked bars, downloads plan artifact.
- Performance sampler expanded to sustained 10-20s window + startup inventory.
- Browser rescue expanded with default-browser/version/proxy/DNS context.
- Printer rescue expanded with spooler/queue/PrintService EVTX + optional restart path.
- Runbook next-step actions polished for export + ticket-summary follow-through.

### Evidence and Exports
- Core evidence collection now includes:
  - system, network, updates, crash, event logs
- Bundle summaries added across collector outputs.
- Validator behavior kept strict by default with explicit user-confirmed override path.

### Reliability
- Runtime import path in `src/app.py` supports module and script/frozen execution.
- Global crash handling writes crash log and provides copy/open-log actions when UI is available.

### QA
- Smoke test extended to include Fix Wi-Fi dry-run and full ticket evidence bundle validation.
