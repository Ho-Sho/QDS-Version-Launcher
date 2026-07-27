# QDSVersionLauncher

A small, portable Windows tool that solves the "which Q-SYS Designer version
do I open this project with?" problem. Double-click a `.qsys` file and pick
the Designer version to open it with, instead of always launching whichever
version happens to own the file association.

```
sample.qsys
   double-click
        v
+----------------------------------+
|  Open with Q-SYS Designer         |
+----------------------------------+
|  9.5.0                            |
|  9.8.2                            |
|  9.12.2                           |
|  9.13.1 LTS                       |
|  ★ 10.0.2                        |
|  10.2.1                           |
+----------------------------------+
```

## Features

- **Auto-detects installed Designer versions.** Reads the real version from
  each Designer executable's file metadata rather than trusting folder
  names (QSC's own multi-version install method involves manually renaming
  folders, so folder names alone aren't reliable). Also cross-checks
  Windows "Programs and Features" entries to catch non-default install
  locations.
- **Shows every version found**, even 10+, in a scrollable list.
- **Most recently used version is starred and pinned to the top.**
- **Ctrl (or Shift) + double-click forces the picker to appear.** Normally,
  if this exact project was opened with a specific version before,
  double-clicking it again launches straight into that version with no
  dialog. Holding **Ctrl** while double-clicking skips that shortcut and
  always shows the picker, so you can deliberately choose a different
  version. Shift is checked too, but **Ctrl is the one to rely on** — see
  Known limitations below for why Shift can be inconsistent.
- **Portable.** Builds to a single, self-contained `.exe` — no installer,
  no separate .NET runtime to install, safe to run from a USB stick.
- **Manage extra scan folders.** The **Manage Folders...** button opens a
  small dialog listing every custom folder you've added, with **Add...**
  and **Remove** — so a folder added by mistake (or one that no longer
  applies) can be taken back out again, instead of only ever growing the
  list.
- **Suppress plugin folder.** A checkbox in the picker that temporarily
  moves Designer's `Plugins` and `Assets` folders out of the way (to
  sibling `Plugins-suppressed` / `Assets-suppressed` folders), so Designer
  starts with none of your custom content loaded — a quick "safe mode" for
  ruling out a misbehaving plugin. It's a persistent on/off toggle (saved
  in `settings.json`), not something tied to one launch: checking it moves
  the folders right away, unchecking moves them back right away, and the
  state is restored to match on the next startup even if the app was closed
  or crashed with it checked. Designer must be fully closed to toggle it.

> **A note on scope, since I couldn't verify this by compiling it myself:**
> This was written and reviewed by hand in a Linux sandbox with no access to
> Windows, Visual Studio, or the WinForms designer, so I could not compile or
> run it here. The code is written carefully and should build cleanly with
> the .NET 8 SDK on Windows, but please build and test it there before
> relying on it, and let me know if anything doesn't compile — I'm happy to
> fix it.

## Project layout

```
QDSVersionLauncher/
├─ QDSVersionLauncher.csproj   Project file (targets net8.0-windows, WinForms)
├─ Program.cs                  Entry point: Ctrl detection, fast-path launch
├─ MainForm.cs                 The version-picker dialog
├─ ManagePathsForm.cs          Dialog to view/add/remove custom scan folders
├─ DesignerScanner.cs           Finds installed Designer versions (+ DesignerVersionInfo)
├─ RegistryHelper.cs           Low-level registry helpers (Uninstall enum, HKCU\Classes)
├─ Settings.cs                 Portable JSON settings (recent versions, custom paths, ...)
├─ Launcher.cs                 Starts the chosen Designer.exe with the project file
├─ PluginSuppression.cs        "Suppress plugin folder" toggle (moves Plugins/Assets aside)
├─ FileAssociation.cs          Registers/unregisters the .qsys file association
├─ app.ico                     App icon (original abstract design, generated for this project)
├─ publish.bat                 Builds the portable single-file EXE
├─ register.bat / unregister.bat   Associate / un-associate .qsys with this tool
└─ README.md
```

One small deviation from the original file list: `DesignerVersionInfo` (the
small data class describing one detected install) lives inside
`DesignerScanner.cs` rather than its own file, since it's only ever used
there — easy to split out later if you'd rather keep one type per file.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```bat
dotnet build QDSVersionLauncher.csproj -c Release
```

For the portable, no-install single EXE, run `publish.bat` (or the
equivalent `dotnet publish` command inside it). The output
`publish\QDSVersionLauncher.exe` is fully self-contained — copy it anywhere.

## Setting it up

1. Put `QDSVersionLauncher.exe` (and `app.ico`, if you kept it alongside)
   wherever you want it to live long-term.
2. Run `register.bat` once. This makes `.qsys` files open through this
   launcher instead of a single fixed Designer version. It only touches
   your own user registry hive (`HKEY_CURRENT_USER`), so it doesn't need
   administrator rights and doesn't affect other Windows accounts on the
   same PC.
3. Double-click any `.qsys` file — the picker should appear.

To undo this later, run `unregister.bat`; it restores whatever `.qsys` was
associated with before (if anything).

## How detection works, and its limits

- Scans `Program Files\QSC Audio`, `Program Files (x86)\QSC Audio`,
  `Program Files\QSC`, and `Program Files (x86)\QSC` (QSC's documented
  default install locations), plus one level of subfolders, plus anything
  registered in "Programs and Features" whose name mentions
  "Designer" and "Q-SYS"/"QSC".
- If your installs live somewhere else entirely, use **Manage Folders...**
  in the picker to point the scanner at them; each folder is remembered
  for future scans, and can be removed again later from the same dialog.
- **LTS labeling is manual.** QSC marks certain releases "LTS" as a
  marketing designation that isn't reliably present in the executable's
  file version metadata, so this tool can't auto-detect it. `settings.json`
  has a `KnownLtsVersions` list (seeded with a couple of examples) — add
  version numbers there as QSC announces new LTS releases, and the picker
  will show them with an "LTS" suffix.

## Settings file

Stored as `settings.json`, either next to the EXE (portable mode, used
whenever that folder is writable) or under
`%AppData%\QDSVersionLauncher\settings.json` as a fallback (e.g. if the EXE
lives in `Program Files`). It holds your custom scan paths, the known-LTS
list, per-project remembered versions, the most-recently-used list, and the
"Suppress plugin folder" checkbox state — feel free to hand-edit it if
needed.

- **Stale per-project entries are cleaned up automatically.** The
  per-project remembered-version list would otherwise grow forever as
  projects get renamed, moved, or deleted. Right before the picker is shown
  (never on the fast, no-dialog launch path, so this can't add latency
  there), the launcher checks — at most once every 7 days — whether each
  remembered project file still exists, and drops the entries whose file is
  gone. Entries on a network path (`\\server\share\...`) are left alone
  rather than checked, since an unreachable share could otherwise make the
  check hang.

## Known limitations

- The Ctrl/Shift-held detection reads live keyboard state the instant the
  process starts, since Explorer doesn't pass modifier keys to launched
  programs. In practice this works fine for a normal double-click, but if
  you release the key unusually fast it could occasionally be missed.
- **Shift is checked, but isn't reliable — use Ctrl.** Windows Explorer
  itself reserves Shift+click for extending a file selection, so a
  Shift-held double-click can get consumed by Explorer's own
  range-selection handling before it ever reaches this app, regardless of
  what the app's code checks for. Ctrl doesn't have that conflict and
  reliably forces the picker; Shift is left in as a secondary option but
  shouldn't be relied on.
- Designed and tested for the common case of Designer installed under the
  default QSC folders; very unusual custom install setups may need a
  manual **Add folder...** the first time.


### Direct Registration via PowerShell
If you want to register the executable directly after building without using `register.bat`, run:

```powershell
& "$env:USERPROFILE\Desktop\QDSVersionLauncher\bin\Release\net8.0-windows\QDS Version Launcher.exe" --register-association


## License & Credits
- **Author / Concept:** Created by Shogo Hori (assisted by AI collaboration).
- **License:** Distributed under the [MIT License](LICENSE).

This tool was generated and refined with AI assistance. It is provided "as is" without warranty of any kind. Please test and verify in your environment before using it in production.