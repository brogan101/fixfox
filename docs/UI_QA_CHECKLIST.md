# UI QA CHECKLIST

## Build + startup
- [ ] `python -m src.tests.smoke` passes.
- [ ] `python -m src.app` launches with no startup traceback.

## Window size and layout
- [ ] Minimum window (`1100x700`): no clipped primary controls.
- [ ] 1080p window (`1920x1080`): balanced spacing and no oversized blank regions.
- [ ] Resize stress test (narrow/wide): scroll areas remain reachable and splitters usable.

## Basic vs Pro layout
- [ ] Switch to Basic: Playbooks shows Guided Goals layout only.
- [ ] Switch to Basic: right concierge panel defaults collapsed.
- [ ] Switch to Basic: Fixes enforces Recommended-first simplified controls.
- [ ] Switch to Basic: Reports locks to Home Share and hides advanced export controls.
- [ ] Switch to Pro: full Playbooks console (Tools/Runbooks + advanced task toggle) is visible.
- [ ] Switch to Pro: Fixes filters + rollback center visible.
- [ ] Switch to Pro: Reports full presets/options visible.

## ToolRunner and status streaming
- [ ] Start a tool/fix/runbook: ToolRunner opens within ~1 second and shows Running state.
- [ ] Live logs stream in ToolRunner while the run is active.
- [ ] Top bar Run Status line 1 shows `Running: <tool>`.
- [ ] Top bar Run Status line 2 shows latest log/progress line + elapsed time.
- [ ] Clicking Run Status opens/focuses ToolRunner.
- [ ] Cancel from top bar and from ToolRunner both stop active long-running tasks.

## Reports stepper
- [ ] No active session: `Run a goal first` empty state shown.
- [ ] Step 1 Configure works and respects mode policy.
- [ ] Step 2 Preview shows tree + redaction/evidence preview.
- [ ] Step 3 Generate updates status and post-export actions.

## Page polish checks
- [ ] Home cards/chips/CTA spacing and typography are consistent.
- [ ] Playbooks detail empty state copy is production quality (no placeholder text).
- [ ] Diagnose empty state includes `Run Quick Check` CTA.
- [ ] Fixes detail never shows unexplained blank commands area.
- [ ] History case summary is labeled and scannable.
- [ ] Settings search/filter and mode/theme updates work instantly.

## Theme + control consistency
- [ ] Palette switch updates all pages consistently.
- [ ] Scrollbars are themed on all scrollable surfaces.
- [ ] Splitter handles are themed and consistent across pages.
- [ ] Accent color usage is restrained to primary actions.

## Release decision (Polisher gate)
- [ ] NO regressions in core outcomes (diagnostics/fixes/runbooks/exports/masking/validator/sessions).
- [ ] No dead buttons or broken context actions.
- [ ] No placeholder copy in user-facing primary flows.
- [ ] Log streaming + status visibility are deterministic.
- [ ] Packaging/docs are complete for store submission.

Decision:
- [ ] GO
- [ ] NO-GO
Notes:
