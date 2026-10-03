---
name: XDM Release Validation
description: Validate XDM changes before release, including version bookkeeping and install checks
---

# XDM Release Validation

## Version bookkeeping (no single source — update all)

- Canonical: `AppInfo.APP_VERSION` in `app/XDM/XDM.Core/AppInfo.cs`.
- Also: `VERSION=` in `app/XDM/XDM.Linux.Installer/{make-deb-pkg,make-rpm-pkg,make-arch-pkg}` and `BUILD_VER=` in `app/XDM/XDM.Win.Installer/make-msi.bat`.
- Stale copies to IGNORE: `app/packaging/` (frozen duplicate), `make-pkg.bat`, `make-silent-msi.bat`, `make-msi - Copy.bat`, `<AssemblyVersion>` in `XDM.Wpf.UI.csproj`.

## Pre-release checklist

1. `dotnet build` clean (0 errors) for `XDM.Gtk.UI` AND `XDM.Wpf.UI`; `node --check` for touched extension files.
2. Runtime smoke on Linux: fresh config start, `/sync` serves, `blockedHosts` correct, `appVersion` matches `APP_VERSION`.
3. `first-run` marker for the new version appears (`~/.xdm-app-data/xdm-<ver>.first-run`).
4. No `binary-source/`, `pkg/`, `src/`, or `*.pkg.tar.*` build artifacts left in the tree (`git status` clean).
5. `git log` messages follow `type(scope): subject` (fix/feat/chore/docs).
6. Installer scripts still executable-text (`make-*` are scripts, not binaries); `.desktop` Exec lines point at the right binary path.

## Linux install validation (pacman host)

- `/opt/xdman` is owned by a pacman package — never hand-copy; build with `make-arch-pkg` and `pacman -U`.
- After install: binary runs with `GTK_USE_PORTAL=1`, binds 8597, autostart entry `~/.config/autostart/xdm-app.desktop` exists and points at the new binary.

## Rules

- Never sign off with unrun checks — mark each item pass/fail/blocked explicitly.
- A red pre-existing baseline item stays red; note it, don't fix it here.
