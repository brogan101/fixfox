# FixFox

FixFox is a Windows WPF desktop app for self-service PC diagnostics and repair workflows.

## What is included

This repository contains the app source, project files, themes, assets, and runtime service code needed to build and run FixFox.

## What is intentionally excluded from the published repo

- Local IDE and agent folders
- Build output and cache folders
- Internal audit and backlog notes
- Test-only projects and helper scripts that are not required to run the app

## Build

```powershell
dotnet build .\HelpDesk.csproj
```

## Run

```powershell
dotnet run --project .\HelpDesk.csproj
```
