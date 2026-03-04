# Release Checklist

1. Run `python -m src.tests.smoke`
2. Run `python -m unittest src.tests.test_unit -v`
3. Launch app with `python -m src.app`
4. Verify no launch warnings and readable QSS
5. Verify Diagnose -> Fix -> Reports -> History flow
6. Verify export validator pass for Home Share and Ticket presets
7. Verify logs path opens and crash handler writes to `%LOCALAPPDATA%\\PCConcierge\\logs`
8. Regenerate catalogs with `python scripts/generate_catalogs.py`
9. Update `CHANGELOG.md` and version in `src/core/version.py`
10. Build EXE via `scripts/build_exe.ps1` (optional release artifact)
