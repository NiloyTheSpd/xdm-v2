# AGENTS.md

## Read this first: the README is wrong

The README says *"This is a standard maven project... `mvn clean install`"*. That is
leftover from the pre-2023 Java codebase. **This repo is now C#/.NET.**

- `README.md` line 43-48 (build instructions) is stale. Ignore it.
- There are **0 `.java` files** and **no `pom.xml`**. The Java tree was ported to C#
  (merge `be9f27f`, "migrate-to-c#").
- `maven.yml` and the other CI workflows were deleted; the only workflow is
  `.github/workflows/xdm-wpf-build.yml`.
- Everything lives under `app/XDM/`. `docs/` is the old project website, not source.

## Commands

The .NET SDK is installed via **mise** and is on `PATH` at
`~/.local/share/mise/shims/dotnet`. If it is missing: `mise use -g dotnet@10.0.401`.
Verified working on Linux with SDK 10.0.401.

### Verified build matrix (Linux, SDK 10.0.401)

| Project | Command | Result |
|---|---|---|
| `XDM.Gtk.UI` (`net6.0`) | `dotnet build app/XDM/XDM.Gtk.UI/XDM.Gtk.UI.csproj` | **OK, 0 errors** (~974 warnings). The Linux app. |
| `XDM.Wpf.UI` (`net4.7.2`) | `dotnet build app/XDM/XDM.Wpf.UI/XDM.Wpf.UI.csproj` | **OK** — emits `xdm-app.exe`; compiles on Linux, cannot *run* there. |
| `XDM.Tests` (`net6.0`) | `dotnet build app/XDM/XDM.Tests/XDM.Tests.csproj` | **OK** |
| `Translations`, `MockServer` | `dotnet build ...` | **OK** |
| `XDM.App.Host`, `NativeMessagingHost` | `dotnet build ...` | **FAILS (pre-existing)** `error NETSDK1124: Trimming assemblies requires .NET Core 3.0 or higher` — both target `net4.7.2` yet set `<PublishTrimmed>true</PublishTrimmed>`. Remove those two lines to fix. |
| `XDM_Tests` (`XDM.SystemTests`) | `dotnet build ...` | **FAILS (pre-existing)** — see test projects below. |
| `MsixPackaging` (`.wapproj`) | — | Windows-only, cannot build on Linux. |

`XDM.Core` has no `.csproj` of its own (it is a shared project) — build it indirectly
via `XDM.Gtk.UI`, which is the fastest full-core compile check on Linux.

**Ignore the `MSB3245 Could not resolve this reference ... GtkSourceSharp/GdkSharp/...`
warnings in `XDM.Gtk.UI`.** Its 8 `<HintPath>D:\gtksharp\...</HintPath>` entries do not
resolve off Windows, but the `GtkSharp` **NuGet package** supplies the same types, so the
build still succeeds. The stale HintPaths are harmless; deleting them would clean up ~8
warnings.

### Tests

`app/XDM/XDM.Tests` is the only runnable suite:

```
dotnet test app/XDM/XDM.Tests/XDM.Tests.csproj
```

**It fails, and it is not your fault.** `JsonParsingTest.cs:156` hardcodes the original
author's desktop:

```csharp
new StreamReader(@"C:\Users\subhro\Desktop\message.json")
```

so it throws `FileNotFoundException` on every machine except that desktop. Verified
identical on pristine `master`. To make it real, commit a small `message.json` fixture and
resolve it via `AppDomain.CurrentDomain.BaseDirectory` or an NUnit temp file.

`app/XDM/XDM_Tests/` (`XDM.SystemTests`, `net5.0`) **cannot compile**: its sources use
`XDM.Core.Lib.*` namespaces that exist nowhere in the repo (the real ones are
`XDM.Core.*`), and its csproj has no `ProjectReference` to `XDM.Core`. Effectively 8 dead
test files. Do not try to run it — port or delete.

CI (`.github/workflows/xdm-wpf-build.yml`, Windows, SDK `6.0.x`) runs from
`./app/XDM/XDM.Wpf.UI/`: `dotnet restore` → `dotnet build --no-restore` →
`dotnet test --no-build`. That directory holds **no test project**, so the `dotnet test`
step exercises nothing.

There is no linter or formatter here. `app/XDM/.editorconfig` only silences `CS8618`,
and there is no `Directory.Build.props`, `global.json` or `nuget.config`. A full
`XDM.Gtk.UI` build emits ~974 warnings — treat that as the baseline, not as regressions.

## Architecture

Two platform front-ends over one shared core, wired by a **static service locator**.

- `XDM.Core/` — all logic. Shared project (see the trap below). Namespaces `XDM.Core.*`.
- `XDM.Core/ApplicationContext.cs` — the service locator. Populated via a fluent builder:
  `ApplicationContext.Configurer().RegisterApplicationWindow(..).RegisterApplication(..)
  .RegisterApplicationCore(..).RegisterCapturedVideoTracker(..).RegisterClipboardMonitor(..)
  .RegisterLinkRefresher(..).RegisterPlatformUIService(..).Configure()`.
  Every getter **throws** if touched before `Configure()`, so bootstrap order matters.
- Entry points, both assembling the same 7 registrations:
  - `XDM.Wpf.UI/App.xaml.cs` — Windows WPF (`StartupObject` `XDM.Wpf.UI.App`, assembly `xdm-app`).
  - `XDM.Gtk.UI/Program.cs` — Linux/macOS GTK3 (assembly `xdm-app`).
  The only platform-specific swap is `IPlatformUIService` (`WpfPlatformUIService` vs
  `GtkPlatformUIService`). UI is abstracted behind `XDM.Core/UI/` interfaces
  (`IApplicationWindow`, `IPlatformUIService`, `IButton`, `IMenuItem`, ...).
- `XDM.Core/Application.cs` wires all main-window events in `AttachedEventHandler()`.
- `XDM.Core/ApplicationCore.cs` — download orchestration (`CoreService`).
- Persistence: SQLite via `System.Data.SQLite.Core`. DB file `downloads.db` under
  `Config.AppDir`; opened in `Application.AppInstance_Initialized`.
  Schema is `CREATE TABLE IF NOT EXISTS` in `XDM.Core/DataAccess/SchemaInitializer.cs` —
  **there is no migration mechanism**, so schema changes need manual handling of existing DBs.
- Config: `~/.xdm-app-data/` (`Config.AppDir`), settings in `settings.dat` via
  `ConfigIO.DeserializeConfig`. The JSON path is commented out. `Config.LoadConfig()`
  must run before anything touches `Config`.

### Target frameworks are all over the place — pick deliberately

| Project | TFM | Platform |
|---|---|---|
| `XDM.Wpf.UI` | `net4.7.2` | `x86` only |
| `XDM.Gtk.UI` | `net6.0` | `x64` |
| `XDM.App.Host`, `NativeMessagingHost` | `net4.7.2` | `x86` |
| `XDM.Msix.*` | `net4.7.2` | Windows |
| `Translations` | `net3.5;net4.5;net4.7.2;net5.0` | — |
| `MockServer`, `XDM.SystemTests` | `net5.0` | — |
| `XDM.Tests` | `net6.0` | — |

`XDM.Gtk.UI` carries 8 hardcoded `<HintPath>D:\gtksharp\GtkSharp-master\BuildOutput\Release\*.dll</HintPath>`
references, but they **do not block the build** — the `GtkSharp` NuGet package supplies
the same types, so it compiles fine on Linux (verified). They only produce `MSB3245`
noise. `XDM.App.Host` and `NativeMessagingHost` genuinely do **not** build (see matrix
above).

## The two traps that will waste your time

### 1. Shared projects need manual `<Compile Include>` entries

`XDM.Core`, `XDM.Messaging`, `XDM.Compatibility` and `NativeMessaging` are legacy
`.shproj` + `.projitems` **shared projects**. They do **not** use SDK-style globbing.
Dropping a new `.cs` file into `XDM.Core/` does nothing until you add a
`<Compile Include="$(MSBuildThisFileDirectory)YourFile.cs" />` line to
`XDM.Core/XDM.Core.projitems`. The build will not warn you — the file is just invisible.

Six files are already stuck in this trap (on disk, **not** in `XDM.Core.projitems`, never
compiled, and with no live references):

```
XDM.Core/AppController.cs
XDM.Core/BrowserMonitoring/CapturedVideoTracker.cs
XDM.Core/BrowserMonitoring/NativeMessagingHostHandler.cs
XDM.Core/IO/DownloadStateStore.cs
XDM.Core/IO/SerializationHelper.cs
XDM.Core/UI/BatchDownloadViewController.cs
```

Treat them as dead code. Do not assume they are live, and do not "fix" them without
asking whether to wire them in or delete them.

Also duplicated: `Translations/TextResource.cs` (standalone `Translations.csproj`, not
referenced by any app) duplicates `XDM.Core/Translations/TextResource.cs`. Only the
`XDM.Core` one is compiled; both declare `namespace Translations`.

### 2. `#if` directives are the platform switch

Cross-platform code in `XDM.Core` is gated by preprocessor symbols — most importantly
`#if NET5_0_OR_GREATER`, which is where **all** Linux/macOS behaviour lives.

Because `XDM.Wpf.UI`, `XDM.App.Host` and `NativeMessagingHost` target `net4.7.2`,
**every Unix branch is compiled out of the Windows build**. Any new cross-platform code
must be wrapped in `#if NET5_0_OR_GREATER`. Symbol usage in `XDM.Core`:
`NET35` (32), `NET5_0_OR_GREATER` (21), `!NET5_0_OR_GREATER` (11), `!NET35` (10),
`WINDOWS` (4).

## TLS certificate validation

Validation is now **on by default** (commit `8898709`). Three bypass sites are gated behind
`XDM_ALLOW_INSECURE_SSL=1` via `PlatformHelper.AllowInsecureSsl`:

- `XDM.Core/Clients/Http/DotNetHttpClient.cs` — `HttpClientHandler.ServerCertificateCustomValidationCallback`.
  **This is the one that mattered.** It is the handler for *all* download traffic on .NET 5+,
  and `HttpClient` ignores `ServicePointManager.ServerCertificateValidationCallback` entirely.
- `XDM.Gtk.UI/Program.cs` and `XDM.Wpf.UI/App.xaml.cs` — `ServicePointManager` callbacks.
  Effective for the .NET Framework build only, because `NetFxHttpClient` uses `HttpWebRequest`,
  which does honour them.

The opt-in is an **environment variable, not a `Config` property, on purpose**: `Config` is
persisted with positional binary serialization (`XDM.Core/IO/ConfigIO.cs` calls `ReadBoolean()`
etc. in strict order), so inserting a field shifts every subsequent read and corrupts existing
`settings.dat` files. If you ever need a real settings toggle, add it at the **end** of both
the serialize and deserialize paths, or use a separate sidecar file.

Do not "tidy" this by adding the flag to `Config`.

## Browser integration

The app runs an HTTP listener on `127.0.0.1:8597` (`NanoServer`, started by
`IpcHttpMessageProcessor`) that the browser extension POSTs to.

- Endpoints: `/sync`, `/download`, `/media`, `/tab-update`, `/vid`, `/clear`, `/link`,
  `/args` — all handled by the switch in `XDM.Core/BrowserMonitoring/IpcHttpMessageProcessor.cs`.
  Every request also returns the config JSON (`OnSyncMessage` runs unconditionally).
- **Port 8597 is hardcoded in 5 places.** Change all of them together:
  `XDM.Core/BrowserMonitoring/IpcHttpMessageProcessor.cs:24` (listener bind),
  `XDM.Core/SingleInstance.cs:41` (instance handoff),
  `XDM.App.Host/Program.cs:337` (`IpcClient.Connect(8597)` — the native-messaging
  host probing for a running app), `chrome-extension/connector.js:4`,
  `firefox-amo/app/connector.js:3`.
  Verify with `grep -rn 8597 app/XDM`; `XDM.Core/BrowserMonitoring/BrowserMonitor.cs:23`
  is a commented-out leftover, ignore it.
- Native messaging host is `xdm-app-host.exe` (built from `XDM.App.Host`).
  `XDM.App.Host/xdm_chrome.native_host.json` pins extension ID
  `akdmdglbephckgfmdffcdebnpjgamofc`, so an unpacked dev extension will **not** match
  unless it carries that ID.
- Extension sources are duplicated: `chrome-extension/` (MV3) and `firefox-amo/app/`
  (MV2). Keep both in sync for behaviour changes.
- Single-instance handoff: a second process POSTs its argv to `/args` on the running
  instance. The mutex name is the Windows-style `Global\XDM_Active_Instance`, shared by
  both entrypoints.

## Version bumps have no single source

Canonical is `AppInfo.APP_VERSION` in `XDM.Core/AppInfo.cs` (currently `8.0.25`). When
bumping, also update the `VERSION=`/`BUILD_VER=` literals in
`app/XDM/XDM.Linux.Installer/{make-deb-pkg,make-rpm-pkg,make-arch-pkg}` and
`app/XDM/XDM.Win.Installer/make-msi.bat`.

Stale copies — **do not edit, and do not treat as current**:

- `app/packaging/` — an older duplicate tree frozen at `8.0.9` (last touched 2022-10-14).
  The `XDM.Linux.Installer` scripts supersede it.
- `make-pkg.bat` (`8.0.18`), `make-silent-msi.bat` (`8.0.14`), `make-msi - Copy.bat` (`8.0.18`)
- `XDM.Wpf.UI.csproj` `<AssemblyVersion>` (`8.0.1`)

### Packaging

`app/XDM/XDM.Linux.Installer/make-{deb,rpm,arch}-pkg` are the canonical Linux packaging
scripts. Each expects `dotnet publish` output already staged in `./binary-source`, then
writes a `.desktop` file, a `/usr/bin/xdman` wrapper, and builds via
`dpkg-deb` / `rpmbuild` / `makepkg`.

Windows installers: `XDM.Win.Installer/make-msi.bat` (WiX `.wxs` files in the same dir).

## Prerequisites that are gitignored

`app/XDM/XDM.Win.Installer/binary-deps/` is **absent from the repo and gitignored**, yet
`XDM.Wpf.UI.csproj` copies it as `Content`. Per `XDM.Win.Installer/ReadMe.txt` you must
supply `ffmpeg-x86.exe` and `yt-dlp_x86.exe` there before an MSI/MSIX build. Without
them the app still builds, but video conversion and yt-dlp video grabbing are
non-functional — so don't chase "broken video" bugs that are just missing binaries.

## Installing on Linux (pacman host)

`/opt/xdman` is owned by the **pacman package `xdman-beta-bin`** (`pacman -Qo /opt/xdman`).
Never hand-copy files over it — pacman tracks them and a later upgrade will fail or silently
revert your build. The correct flow is to build a package and reinstall it.

- This repo's `make-deb-pkg` / `make-rpm-pkg` need `dpkg-deb` / `rpmbuild`, which are **absent**
  on an Arch host. `make-arch-pkg` uses `makepkg`, which is present.
- `dpkg-deb` not found is therefore expected here, not a broken script — the script still
  produces its whole staging tree before it invokes the packer.
- Replacing the installed package needs `sudo`, which prompts for a password. Plan around it:
  build and verify as a normal user in `~/`, then do only the final `pacman -U` as root.
- Run the app as `GTK_USE_PORTAL=1 /opt/xdman/xdm-app --background`, which is what the shipped
  `xdman-beta.desktop` does.

## Debugging

Set `XDM_DEBUG_MODE=1` to enable file logging; both entrypoints write `log.txt` into
`Config.AppDir` (`~/.xdm-app-data/log.txt`). Without it, `Log.Debug` calls go nowhere.

**Caveat that will waste your time if you trust it:** on the GTK build this often produces
**no `log.txt` at all**, even with the env var correctly set (confirmed: `XDM_DEBUG_MODE=1`
present in `/proc/<pid>/environ`, file still absent). The gate is compile-time —
`Log.InitFileBasedTrace` adds a `TextWriterTraceListener`, but `Trace.*` calls only survive if
`TRACE` is in `<DefineConstants>`, and `XDM.Gtk.UI.csproj` defines only `LINUX`
(`XDM.Wpf.UI.csproj` does define `TRACE;WINDOWS`). So on Linux, treat stderr plus the
`/sync` IPC endpoint as your observability, not `log.txt`. Fixing this means adding `TRACE`
to the Gtk project's `DefineConstants`.

Runtime data: `~/.xdm-app-data/` — `settings.dat`, `downloads.db`, `temp/`,
`xdm-<version>.first-run` (first-run detection), `log.txt`. Delete the `first-run` file
to re-trigger the first-run flow.

## Translations

`app/XDM/Lang/*.txt` are `KEY=value` files; `Lang/index.txt` maps display name → filename
and is loaded from `AppDomain.CurrentDomain.BaseDirectory/Lang`. `Lang/English.txt` is
the base file.

**`TextResource.GetText(key)` returns an empty string for an unknown key — not the key
itself.** A missing or misspelled key renders as blank UI with no error, which is easy to
misread as a layout bug. When adding a UI string, add the key to `Lang/English.txt`.

`translation-generator/translation-gen/` is a separate Create React App (npm) for editing
translations; it is not part of the .NET build.

## Conventions

- `<Nullable>enable</Nullable>` on every project; `LangVersion` is `9.0` except
  `XDM.Gtk.UI`, which uses `latest`. Prefer nullable annotations over `!`, but match
  the surrounding style.
- Logging is `TraceLog.Log.Debug(...)` from `XDM.Core/TraceLog/Log.cs`; there is no
  ILogger/DI container. DI is the `ApplicationContext` service locator.
- UI-thread marshalling goes through `Application.RunOnUiThread` /
  `IApplicationWindow.RunOnUIThread`; download callbacks arrive on background threads.

## Repo state

This is our own fork/derivative of `subhra74/xdm`, worked on in place — improvements are
ours, not upstream PRs. `master` is the working branch. Upstream is in active churn, so
recent commits in `XDM.Core/` are the best guide to current intent; prefer matching the
nearest surrounding code over the README or older Java-era conventions.

Version is `8.0.30` (`AppInfo.APP_VERSION`), deliberately above the `8.0.29` currently
installed from upstream binaries so our builds never look stale. `app/packaging/` is a dead
duplicate frozen at `8.0.9`; `app/XDM/XDM.Linux.Installer/` is canonical.

## Merged upstream PRs (branch `merge/upstream-prs-2026-10`)

Upstream's open PRs are almost all **Java-era and unmergeable** — they patch `*.java` /
`pom.xml` deleted in the C# port, or translation files using the pre-rename names
(`ar.txt`, `map`, `en.txt` vs today's `Arabic.txt`, `index.txt`). Do not re-attempt them.

Merged and verified: `#1324` (HLS `_chunks = null` on restore failure), `#1176`
(`thwawte.com` → `thawte.com` in `DefaultBlockedHosts`), `#1127` Sinhala, `#1321` Hindi
(malformed key `एमएसजी_ओके = ओके` → `MSG_OK`, which silently rendered blank),
`#1186` Turkish 48→261 keys, `#889` README, and dependabot `#864 #866 #867 #935 #945 #974
#1003` (lockfile-only; clears CVE-2021-44906, CVSS 9.8, in `minimist`).

Note `#1176` fixed a bug that **upstream 8.0.29 still has** — verify with
`strings -el /opt/xdman/xdm-app.dll | grep thwawte`.

### Browser extensions need NO rebuild for app-side changes

The extension fetches its config from the app at runtime —
`chrome-extension/app.js:39` and `firefox-amo/app/app.js:35` both do
`this.blockedHosts = msg.blockedHosts` from the `/sync` response. Nothing is hardcoded, so
fixing `Config.cs` fixes the shipped extension. Only touch the extension if you change the
IPC contract (endpoints `/sync /download /media /tab-update /vid /clear /link /args`, or the
JSON shape).