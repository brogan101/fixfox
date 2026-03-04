# Runner Log Streaming Fix (2026-03-04)

## Goal
Ensure all task execution paths stream live output to:
1. ToolRunner popup (primary execution surface)
2. Top-bar run status area (latest line + elapsed)

## Architecture

### Run Event Bus
File: `src/core/run_events.py`

Event types now include:
- `START`, `PROGRESS`, `STATUS`, `STDOUT`, `STDERR`, `ARTIFACT`, `WARNING`, `ERROR`, `END`

Capabilities:
- Per-run ring buffer for replay (`events_since(...)`)
- Per-run subscribe (`subscribe(run_id, ...)`)
- Global subscribe (`subscribe_global(...)`) for app-wide status widgets
- Late subscriber replay for run-specific subscriptions

### Producers

#### Subprocess command runner
File: `src/core/command_runner.py`
- Emits `START` immediately on launch.
- Streams stdout/stderr line-by-line from dedicated pump threads.
- Emits heartbeat `PROGRESS` + `STATUS` updates each second while running.
- On timeout/cancel:
  - kills process tree
  - emits `WARNING` + `STATUS`
  - emits final `END` with return code (`124` timeout, `130` cancelled)

#### Script tasks and runbooks
Files:
- `src/core/script_tasks.py`
- `src/core/runbooks.py`

Both now emit `STATUS` transitions (start, step-level updates, success/failure/cancel completion).

### Consumers

#### ToolRunner window
File: `src/ui/components/tool_runner.py`
- Subscribes to run-specific bus events.
- Uses `QPlainTextEdit` for high-volume log appends.
- Appends lines via signal (`append_output_requested`) on the UI thread.
- Supports pause/resume output and auto-scroll toggle.
- Shows running placeholder + elapsed when no output yet.

#### Top-bar run status
File: `src/ui/main_window.py`
- Subscribes once to global bus events.
- Filters to active run id.
- Tracks:
  - run state (`Ready`/`Running`/`Success`/`Failed`/`Cancelled`)
  - latest log line (`STDOUT`/`STDERR`/`WARNING` preferred)
- Clicking the status card opens/focuses ToolRunner.

## Cancel behavior
- `Cancel Task` calls worker cancellation.
- Command runner terminates process tree.
- Bus emits terminal status (`Cancelled`) and `END` code `130`.
- ToolRunner and top-bar state transition to cancelled.

## Debugging playbook (if output is missing)
1. Confirm run id exists in ToolRunner:
- ToolRunner must be opened with non-empty `run_id` and `event_bus`.

2. Inspect event stream:
- Use `events_since(run_id, 0)` and verify presence of `START` and `END`.
- Verify at least one of: `STATUS`, `PROGRESS`, `STDOUT`, `STDERR`, `WARNING`, `ERROR`.

3. Validate producer wiring:
- Ensure task path publishes into the shared bus (same `run_id`).
- For command paths, verify command runner is passed both `event_bus` and `run_id`.

4. Validate consumer wiring:
- ToolRunner: `attach_event_bus(...)` called and subscription active.
- MainWindow: global subscription active and `active_run_id` set.

5. Confirm UI state:
- If logs appear only after completion, check for non-streaming subprocess usage.
- If logs stop while scrolled up, confirm auto-scroll state (manual mode may be active).

## Verification run
- Smoke tests pass via `.\\.venv\\Scripts\\python.exe -m src.tests.smoke`.
- Manual behavior check completed:
  - run starts -> ToolRunner opens
  - live lines stream in ToolRunner
  - top bar updates with last line + elapsed
