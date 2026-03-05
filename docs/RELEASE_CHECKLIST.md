# Release Checklist

- [ ] Brand source exists: `src/assets/brand/fixfox_logo_source.png`
- [ ] Brand derivatives exist: `fixfox_mark.png`, `fixfox_mark@2x.png`, `fixfox_icon.ico`
- [ ] Required icons exist in `src/assets/icons/`
- [ ] `python scripts/ui_walkthrough.py` passes
- [ ] `python scripts/verify_requirements.py` passes
- [ ] `python -m src.tests.smoke` passes
- [ ] `python -m src.tests.test_unit` passes
- [ ] `python -m src.tests.test_requirements_gate` passes
- [ ] Working tree clean (`git status --porcelain`)
- [ ] Version/changelog updated where applicable

