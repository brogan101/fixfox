# Privacy

## What Fix Fox Collects
- Session metadata (session id, symptom, timestamps)
- Diagnostic findings and action results
- User-triggered evidence artifacts (logs, snapshots, run outputs)
- Export package metadata (manifest + hashes)

## What Fix Fox Does Not Collect by Default
- No cloud telemetry upload
- No background data exfiltration
- No remote command-and-control channel

## Storage Location
- `%LOCALAPPDATA%\FixFox\`
  - `sessions/`
  - `exports/`
  - `logs/`

## Share-Safe Masking
- Applied to:
  - copied summaries/output
  - report/session text artifacts
  - export package text payloads
- Scans for sensitive patterns (host/user path/SSID tokens) before validation completes.

## User Control
- You decide when to run tools, collect evidence, and export packages.
- You can remove local files manually from app-data folders.
