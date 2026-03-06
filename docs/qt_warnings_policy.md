# Qt Warnings Policy

FixFox treats the following Qt warning patterns as **fatal** for stabilization gates:

- `Could not parse application stylesheet`
- `Unknown property` (except `qproperty-*` compatibility fields)
- `Failed to create DirectWrite face`
- `Cannot open file ...`
- `Cannot find font directory ...`

Enforcement points:

- `scripts/qss_sanity_check.py` fails if any fatal pattern is emitted.
- `src/tests/test_app_launch.py` fails if any fatal pattern is emitted during window launch.
- `src/core/startup_watchdog.py` captures Qt messages to `logs/fixfox_qt.log` and logs fatal lines.

Rationale:

- These warnings correlate with broken styling, missing assets/fonts, or startup instability.
- Stabilization pass requires zero stylesheet parse noise and deterministic launch behavior.
