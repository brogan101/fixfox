# FixFox

FixFox is a Windows desktop support app for diagnosing common PC problems, running explainable repairs, and packaging clean support evidence when self-service is not enough.

## What FixFox does well

- Runs a quick health scan and shows the next safe action.
- Diagnoses plain-English problems with Guided Diagnosis.
- Launches verified repairs, maintenance profiles, and guided workflows.
- Opens the exact Windows tool or settings page a technician would use.
- Creates support packages with local receipts, health context, and issue summaries.

## What FixFox is not

- It is not a registry cleaner.
- It does not require an account.
- It does not upload telemetry or support bundles automatically.
- It does not replace IT when the real problem is identity, policy, permissions, certificates, or hardware failure.

## Supported platform

- Windows 10 version 1903 or later
- Windows 11
- .NET 8 SDK only needed when building from source

## Build from source

```powershell
dotnet build .\HelpDesk.csproj -c Release
.\bin\Release\net8.0-windows\FixFox.exe
```

## Package a customer-ready build

```powershell
.\Build-FixFox.ps1
```

That pipeline:

- validates the source tree
- builds Release
- runs automated tests
- publishes a self-contained Win64 build
- runs the built-in headless verifier
- stages a portable package in `dist\`
- builds an installer too if Inno Setup 6 is installed locally

## First launch

The first-run setup explains:

- what FixFox does
- what stays local
- how profiles affect behavior
- how tray mode works
- how to run the first health check

The packaged app ships with local guides in `Docs\`.

## Local data

FixFox stores its working data under:

`%APPDATA%\FixFox`

That includes:

- settings
- logs
- repair receipts
- support packages you explicitly create
- startup verification output
- interrupted-work recovery state

## Updates and release notes

- The in-repo build uses a local manifest release feed in `Configuration\release-feed.json`.
- Release notes ship as `CHANGELOG.md`.
- If the configured update source is unavailable, FixFox stays usable and reports that cleanly in-app.

## Tests

```powershell
dotnet test .\HelpDesk.Tests\HelpDesk.Tests.csproj -c Release
```

## Headless verification

```powershell
.\bin\Release\net8.0-windows\FixFox.exe --verify-headless
```

This validates startup-critical wiring such as:

- settings load/save and recovery
- fix catalog integrity
- runbook registration
- theme resources
- update provider readiness
- knowledge base readiness

## Customer docs

- [SETUP.md](SETUP.md)
- [Docs/Quick-Start.md](Docs/Quick-Start.md)
- [Docs/Privacy-and-Data.md](Docs/Privacy-and-Data.md)
- [Docs/Support-Packages.md](Docs/Support-Packages.md)
- [Docs/Recovery-and-Resume.md](Docs/Recovery-and-Resume.md)
- [Docs/Troubleshooting-and-FAQ.md](Docs/Troubleshooting-and-FAQ.md)

## Repository note

The shipping product identity is `FixFox`. The internal root namespace is still `HelpDesk`, which is a legacy technical name rather than the customer-facing product name.
