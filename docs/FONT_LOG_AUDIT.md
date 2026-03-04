# Font Warning Audit and Fix Log (2026-03-04)

## Scope
Goal: remove launch/runtime spam:
- `QFont::setPointSize: Point size <= 0 (-1)`

## Phase 0 audit (before fixes)
Commands used:
- `rg -n "QFont|setPointSize|pointSize|setPixelSize|pixelSize|QFontDatabase|setFont" src`
- `rg -n "font-size:\\s*[^;]+" src/ui/app_qss.py`

Findings:
- No direct invalid `setPointSize(...)` usage in app code.
- Global QSS typography in `src/ui/app_qss.py` used pixel units (`px`) for all major text roles.
- ToolRunner explicitly set a monospace font (`QFont("Consolas")`) without normalizing point size.

## Root cause hypothesis
Qt can carry fonts in pixel-sized/unspecified point-size state (`pointSize() == -1`) when stylesheets are pixel-based. Any point-size code path (including Qt internals and copied fonts) can then trigger:
- `QFont::setPointSize: Point size <= 0 (-1)`

Evidence:
- Pre-fix QSS `font-size` used only `px`.
- Warning text explicitly references invalid point-size path.

## Fixes applied

### 1) Convert app QSS typography to point units
File: `src/ui/app_qss.py`
- Replaced all typography `font-size: ...px` with `font-size: ...pt`.
- Current declarations are point-based at:
  - line 35, 58, 63, 94, 99, 102, 106, 187.

### 2) Add font safety helper
New file: `src/ui/font_utils.py`
- `clamp_point_size(ps, default_ps=12)`
- `safe_copy_font(base, default_ps=12)`
- Safety call is centralized at line 14 (`setPointSize(clamp_point_size(...))`).

### 3) Use safe font copy in ToolRunner
File: `src/ui/components/tool_runner.py`
- Replaced direct monospace font assignment with:
  - `safe_copy_font(QFont("Consolas"), default_ps=10)` (line 110)

## Verification

### Automated tests
- Command run: `.\\.venv\\Scripts\\python.exe -m src.tests.smoke`
- Result: `Smoke test passed.`

### Startup warning check
- Offscreen startup/quit script run for `MainWindow` initialization.
- Result output: `APP_STARTUP_CHECK_OK`
- No `QFont::setPointSize` warnings emitted during the launch check.

## Outcome
Font warning root cause was addressed by normalizing app typography units to points and clamping explicit font copies. Launch warning spam is no longer observed in verification runs.
