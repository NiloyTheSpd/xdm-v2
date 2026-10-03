---
name: XDM Issue Investigation
description: Investigate a GitHub issue against the actual XDM codebase and determine the root cause before any fix is proposed
---

# XDM Issue Investigation

Use when triaging or fixing a bug report from `subhra74/xdm` (or this fork).

## Workflow

1. Read the complete issue, including all comments: `gh issue view <N> --repo subhra74/xdm --json number,title,body,comments`.
2. Classify first, fix never (yet):
   - `XDM.Core/` uses namespaces `XDM.Core.*`. If the report references `XDM.Core.Lib.*`, `*.java`, or `pom.xml`, it targets the deleted Java tree or a stale test project — verify with `find app -name '*.java'` (expect 0) and close as stale.
   - Translation reports referencing short names (`ar.txt`, `map`, `en.txt`) are pre-port. Current files are Title Case under `app/XDM/Lang/` plus `index.txt`.
   - `app/xdm-browser-monitor--depricated/` is dead. Live extensions are `app/XDM/chrome-extension/` (MV3) and `app/XDM/firefox-amo/app/` (MV2).
3. Locate the code path. Key maps:
   - Startup: `XDM.Gtk.UI/Program.cs` → `ApplicationContext.Configurer()…Configure()` → `XDM.Core/Application.cs:AppInstance_Initialized`.
   - Browser IPC: `127.0.0.1:8597` via `IpcHttpMessageProcessor.cs` (endpoints `/sync /download /media /tab-update /vid /clear /link /args`); extension side in `connector.js` + `app.js` + `request-watcher.js`.
   - Downloads: `ApplicationCore.cs` → `XDM.Core/Downloader/{Progressive,Adaptive}/`.
   - Persistence: `Config.cs` + positional-binary `XDM.Core/IO/ConfigIO.cs`; SQLite `XDM.Core/DataAccess/`.
4. Reproduce when reasonably possible. Note the observability limits:
   - `XDM_DEBUG_MODE=1` file logging requires `TRACE` in the project's `DefineConstants` (GTK has it; verify before trusting a missing `log.txt`).
   - The `/sync` endpoint is a live probe: `curl -X POST http://127.0.0.1:8597/sync -H 'Content-Type: application/json' -d '{}'`.
   - `TextResource.GetText` returns empty string for unknown keys — blank UI means a missing key, not a layout bug.
5. Deliver: classification (Critical/High/Medium/Low/Duplicate/Stale/Env-specific/Not-reproducible), root cause with file:line evidence, and the smallest fix shape. Do NOT implement the fix under this skill — hand off to `code-architect` or `bug-fixer`.

## Rules

- Never trust the issue title; verify every claim against the repo.
- A missing reproduction is a finding, not a failure — document it and stop.
- One hypothesis at a time; quote the exact lines that support or refute it.
