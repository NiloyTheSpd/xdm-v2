---
name: XDM Regression Testing
description: Run the appropriate build, tests, and validation checks after changing XDM code
---

# XDM Regression Testing

Run after any code change, before declaring done. Requires .NET SDK 10.0.401 via mise (`~/.local/share/mise/shims/dotnet` on PATH).

## Commands

```bash
export PATH="$HOME/.local/share/mise/shims:$PATH"
# Fastest full-core compile check (XDM.Core is a shared project, build it via Gtk):
dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj
# Windows UI (compiles on Linux, cannot run there):
dotnet build app/XDM/XDM.Wpf.UI/XDM.Wpf.UI.csproj
# JS syntax for extension changes:
node --check app/XDM/chrome-extension/app.js app/XDM/chrome-extension/request-watcher.js \
  app/XDM/chrome-extension/logger.js app/XDM/firefox-amo/app/app.js \
  app/XDM/firefox-amo/app/request-watcher.js
```

## Baselines (do NOT chase these as regressions)

- `XDM.Gtk.UI` build emits ~974 warnings (CS0649 glade-field noise, CA1416 platform notes, MSB3245 stale HintPaths). Only `error` lines matter.
- `XDM.App.Host` / `NativeMessagingHost` fail with pre-existing `NETSDK1124` (trimming on net472). `XDM_Tests` cannot compile (`XDM.Core.Lib.*` namespaces don't exist).
- `dotnet test app/XDM/XDM.Tests` fails with `FileNotFoundException` for a hardcoded `C:\Users\subhro\...` path — pre-existing, unrelated to your change unless you touched that file.

## Runtime smoke (Linux, needs a display session)

```bash
# Stop any running instance first via its own IPC, never kill -9 (stale mutex):
curl -s -X POST http://127.0.0.1:8597/args -H 'Content-Type: application/json' -d '["--quit"]'
DOTNET_ROLL_FORWARD=Major XDM_DEBUG_MODE=1 GTK_USE_PORTAL=1 ./xdm-app
# Then: port 8597 bound, /sync returns config JSON, log.txt appears in ~/.xdm-app-data
```

## Rules

- Never report "tests passed" unless the command actually ran green.
- If a check cannot run (no display, no SDK), state exactly which one and why.
- `git status` must show only intended files; `bin/`/`obj/` are gitignored — never commit them.
