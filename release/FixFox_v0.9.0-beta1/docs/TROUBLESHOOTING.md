# Troubleshooting

## App Won't Start
1. Run with project interpreter:
   - `.venv\Scripts\python.exe -m src.app`
2. Install dependencies:
   - `.venv\Scripts\python.exe -m pip install -r requirements.txt`
3. Check logs:
   - `%LOCALAPPDATA%\FixFox\logs\app.log`
   - `%LOCALAPPDATA%\FixFox\logs\crash.log`

## Export Validation Failed
- Open Reports and review validation warning details in ToolRunner.
- Fix obvious masking/evidence issues and retry.
- Use explicit override path only when operationally required.

## Admin Task Failed
- Verify you launched with administrative privileges.
- Re-run with dry-run first for runbooks.
- Check ToolRunner `Details` + logs for deterministic next steps.

## WLAN / EVTX Artifacts Missing
- Some Windows environments restrict event/log export access.
- Artifacts are best-effort by design and summarized in bundle outputs.

## UI Feels Unresponsive
- Long tasks should run in ToolRunner with progress/cancel.
- If frozen due external command hang, cancel in ToolRunner and re-run with smaller scope.

## Where to Send for Support
- Export `ticket` pack from Reports.
- Include:
  - ZIP
  - ticket summary (short + detailed)
  - symptom + exact observed failure steps
