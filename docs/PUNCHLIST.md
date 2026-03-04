# PUNCHLIST

Source: `docs/AUDIT_REPORT.md` (2026-03-03)

## Phase 1 Critical
- [x] Enforce module-only app launch (`python -m src.app`).
- [x] Make `python src/app.py` print friendly guidance and exit.
- [x] Remove sys.path injection fallbacks from runtime/catalog scripts.
- [x] Re-run smoke tests and startup checks.

## Phase 2 UI Foundation Alignment
- [x] Rename `Toolbox` navigation taxonomy to `Playbooks` while keeping nav count <= 7.
- [ ] Centralize row context-menu construction in `ui/components/context_menu.py`.
- [ ] Add local `?` help entrypoint in page headers.

## Phase 3 Core Contract Hardening
- [ ] Upgrade registry to typed, executable entities and authoritative discoverability/execution.
- [ ] Implement deterministic masking placeholders (`<PC_1>`, `<USER_1>`, `<SSID_1>`, optional `<IP_1>`).
- [ ] Move export output to canonical support-pack folder structure.
- [ ] Strengthen export validator and manifest/hash integrity checks.

## Phase 4+ Platform Expansion
- [ ] Add Runbook Studio (builder/import/export/validation).
- [ ] Add plugin loader + plugin manager UI + catalog generation.
- [ ] Add CLI (`pcconcierge-cli`) run/export/list commands.
- [ ] Add fleet pack aggregator under Reports.
- [ ] Add MSI/MSIX/signing/update pipeline stubs and docs.
- [ ] Add optional parallel QML frontend (`--ui widgets|qml`).
