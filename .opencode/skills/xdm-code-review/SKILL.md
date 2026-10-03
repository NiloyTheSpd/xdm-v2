---
name: XDM Code Review
description: Review XDM changes for correctness, regressions, architecture fit, and maintainability
---

# XDM Code Review

Review the working-tree diff (`git diff`, `git status`). Read-only: do not modify files.

## Checklist

1. **Shared-project trap**: new `.cs` files under `XDM.Core/` (or Messaging/Compatibility/NativeMessaging) must have a matching `<Compile Include>` in the corresponding `.projitems`, or they don't compile in.
2. **#if gating**: cross-platform code wrapped in `#if NET5_0_OR_GREATER` where the net472 builds need it compiled out? No Unix-only API leaking into the WPF path (check CA1416 warnings introduced).
3. **Config serialization**: `XDM.Core/IO/ConfigIO.cs` is positional-binary. New `Config` fields must go at the END of both serialize and deserialize paths, or use a sidecar — never insert mid-stream (corrupts existing `settings.dat`). Prefer env vars for niche flags; see `PlatformHelper.AllowInsecureSsl` precedent.
4. **UI thread**: download callbacks arrive on background threads; UI touches must go through `Application.RunOnUiThread` / `IApplicationWindow.RunOnUIThread`. Flag any direct UI access from `Downloader/`, `BrowserMonitoring/`, or `IpcHttpMessageProcessor.Run` (runs on its own thread).
5. **Null windows**: `GtkHelper.ShowMessageBox(window, …)` and friends must tolerate null; `ShowMessageBox(null, …)` from a background thread is a known crash shape.
6. **IPC contract**: changes to `/sync /download /media /tab-update /vid /clear /link /args` paths or the config JSON shape require matching extension changes in BOTH `chrome-extension/` and `firefox-amo/app/`. Port 8597 appears in 5 places — grep to confirm.
7. **Translations**: new UI strings need keys in `Lang/English.txt`; other languages may lag (they render blank for missing keys — never treat blank UI as a layout bug without checking keys first).
8. **TLS**: no new `CertificateValidationCallback … => true` or equivalent bypass without an explicit opt-in gate.
9. **Scope**: one concern per change; no drive-by refactors; public behavior preserved unless the issue demands a break.

## Output

Findings in severity order (Blocker/Major/Minor), each with `file:line` and the concrete failure it would cause. Distinguish "will break" from "might break" from "style".
