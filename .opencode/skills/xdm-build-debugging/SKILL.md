---
name: XDM Build Debugging
description: Diagnose platform, dependency, compiler, or packaging problems in the XDM .NET build
---

# XDM Build Debugging

## Facts that resolve most build mysteries

- `XDM.Core`, `XDM.Messaging`, `XDM.Compatibility`, `NativeMessaging` are legacy `.shproj`+`.projitems` shared projects with **explicit `<Compile Include>` lists**. A new `.cs` file that isn't listed is silently excluded — the build won't warn. Check `XDM.Core/XDM.Core.projitems` first when a type "doesn't exist".
- Platform code is `#if`-gated. `NET5_0_OR_GREATER` holds all Unix branches; the `net4.7.2` projects (WPF, App.Host, NativeMessagingHost) compile those out. New cross-platform code must be wrapped.
- Target-framework matrix: Gtk `net6.0`, WPF/App.Host/NativeMessagingHost `net4.7.2`, Tests `net6.0`, Translations multi-target, MockServer/SystemTests `net5.0`. EOL warnings (NETSDK1138) are expected noise.
- `XDM.Gtk.UI` has 8 stale `<HintPath>D:\gtksharp\…</HintPath>` entries producing MSB3245 warnings; the GtkSharp NuGet package supplies the types, so the build succeeds regardless. Do not "fix" by deleting unless verifying a clean rebuild after.
- No `Directory.Build.props`, `global.json`, or `nuget.config` exist. No linter/formatter beyond `.editorconfig` (silences CS8618 only).

## Diagnosis workflow

1. Reproduce with `-v q` and isolate: is it `error` (blocking) or `warning` (baseline noise)?
2. `error NETSDK1124` on App.Host/NativeMessagingHost → `<PublishTrimmed>true</PublishTrimmed>` on a net472 target; remove those lines.
3. Missing-type errors in `XDM.Core` → check the `.projitems` include list.
4. `#if`-related surprises → check which TFM the failing project targets and which symbols that defines.
5. Packaging failures: `make-deb-pkg` needs `dpkg-deb`, `make-rpm-pkg` needs `rpmbuild` — both absent on Arch (expected, not a bug); `make-arch-pkg` needs `makepkg` (present). All scripts expect `dotnet publish` output pre-staged in `./binary-source`.
6. `dotnet` missing → `mise use -g dotnet@10.0.401`; csharp-ls additionally needs `DOTNET_ROOT=/home/thespd/.local/share/mise/dotnet-root` for MSBuildLocator.

## Rules

- Quote the exact error code and the failing project before theorizing.
- Verify the fix with a rebuild of the same project, not a different one.
