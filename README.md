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
|  ★ 10.0.2                        |
|  10.4.1                           |
|  10.2.1                           |
|  9.13.1 LTS                       |
|  9.12.2                           |
|  9.8.2                            |
|  9.5.0                            |
+----------------------------------+
```

> ## ⚠️ Read this first: association order matters
>
> **Run `register.bat` *before* touching Windows' own "Open with" / Default
> apps UI for `.qsys`.** Doing it the other way around is the #1 cause of
> `.qsys` files permanently showing this app's own icon (`app.ico`) instead
> of the intended `qsys.ico`, even after `register.bat` is run afterward.
>
> Here's why: if you associate `.qsys` with this launcher through Windows
> Settings → Default apps (or Explorer's "Open with" → "Look for another
> app on this PC" → browsing to the `.exe` directly) **before**
> `register.bat` has ever run, Windows creates its own separate
> `Applications\<exe>` registry entry for the raw `.exe` instead of using
> the ProgID this app registers. That entry has no custom icon, so it
> falls back to the app's own icon — permanently, since re-running
> `register.bat` later updates the *other* (correct) registry entry, not
> the one Windows is actually using once that choice has been made.
>
> **Correct order:**
> 1. Run `register.bat` first. On its own this is enough — double-clicking
>    a `.qsys` file will already open it through this launcher, with the
>    correct `qsys.ico`, no further setup needed.
> 2. If Windows still shows "How do you want to open this file?" (e.g. the
>    very first time you double-click one), pick **QDS Version Launcher**
>    from the list shown. Don't use "Look for another app on this PC" to
>    browse to the `.exe` — that's what creates the broken entry above.
>
> Already stuck with the wrong icon? Reset the association first: **Settings
> → Apps → Default apps**, search `.qsys`, reset/remove the current choice,
> then repeat the two steps above.

## Features

- **Auto-detects installed Designer versions.** Reads the real version from
  each Designer executable's file metadata rather than trusting folder
  names (QSC's own multi-version install method involves manually renaming
  folders, so folder names alone aren't reliable). Also cross-checks
  Windows "Programs and Features" entries to catch non-default install
  locations.
- **Shows every version found**, even 10+, in a scrollable list.
- **Most recently used version is starred and pinned to the top.**
- **Configurable shortcut for forcing the picker to appear.** Normally, if
  this exact project was opened with a specific version before,
  double-clicking it again launches straight into that version with no
  dialog. The picker has a **"Show picker on:"** section where you choose
  what forces it open instead: **Always**, **Ctrl is held**, **Shift is
  held**, or **Ctrl + Shift are held**. The choice saves the moment you
  pick it. Default is **Ctrl**, and it's the one to rely on — Windows
  Explorer itself uses Shift+click for extending a file selection, so a
  Shift-held double-click can get eaten by that before it ever reaches
  this app.
- **Pin a project to always skip the picker.** An "Always use this version
  for this project" checkbox in the picker overrides the mode above on a
  per-project basis — check it and click Open to make that project always
  launch straight into the selected version, even in **Always** mode.
  Holding Ctrl or Shift still forces the picker open so the pin can be
  changed later.
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
- **One-click Clean Registry button.** Does the same thing as
  `unregister.bat` (un-associates `.qsys` files from this launcher,
  restoring whatever handled them before, if anything) but reachable
  straight from the picker window, so you don't have to go find the batch
  file before deleting or uninstalling the app.
- **Shows its own version number.** The bottom-right corner of the picker
  displays something like `ver1.0.1`, so it's easy to tell at a glance —
  or when reporting an issue — exactly which build is running. It's read
  from the exe's own version metadata, which comes straight from
  `<Version>` in the `.csproj`, so bumping that one property is the only
  thing needed to change what's shown.

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
├─ Program.cs                  Entry point: modifier-key detection, fast-path launch
├─ MainForm.cs                 The version-picker dialog
├─ ManagePathsForm.cs          Dialog to view/add/remove custom scan folders
├─ DesignerScanner.cs           Finds installed Designer versions (+ DesignerVersionInfo)
├─ RegistryHelper.cs           Low-level registry helpers (Uninstall enum, HKCU\Classes)
├─ Settings.cs                 Portable JSON settings (recent versions, custom paths, ...)
├─ Launcher.cs                 Starts the chosen Designer.exe with the project file
├─ PluginSuppression.cs        "Suppress plugin folder" toggle (moves Plugins/Assets aside)
├─ FileAssociation.cs          Registers/unregisters the .qsys file association
├─ app.ico                     App icon, embedded in the EXE and shown in the picker window
├─ qsys.ico                    Optional Explorer icon for .qsys files themselves (not the app)
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

## Installing

1. Put `QDSVersionLauncher.exe` (and `app.ico`, if you kept it alongside)
   wherever you want it to live long-term. `qsys.ico` is optional — include
   it too if you want `.qsys` files themselves to show a distinct icon in
   Explorer, separate from the app's own icon.
2. Run `register.bat` once. This makes `.qsys` files open through this
   launcher instead of a single fixed Designer version. It only touches
   your own user registry hive (`HKEY_CURRENT_USER`), so it doesn't need
   administrator rights and doesn't affect other Windows accounts on the
   same PC. **Do this before touching Windows' own "Open with" / Default
   apps UI for `.qsys`** — see the warning near the top of this file for
   why the order matters.
3. Double-click any `.qsys` file — the picker should appear.

## Uninstalling

This app has no installer and never appears in "Programs and Features" —
everything it touches lives in your own user registry hive or in its own
files, so removing it is just a few steps:

1. **Undo the file association first, while the EXE still exists.** Click
   **Clean Registry** inside the picker, or run `unregister.bat` (both do
   the same thing). This restores whatever `.qsys` was associated with
   before this app (if anything) and clears the stale "Recommended apps"
   cache entry Windows sometimes keeps for the exe.
   - If you also set `.qsys`'s default app through Windows' own Settings →
     Default apps UI at some point (see the warning near the top), reset
     that separately — `unregister.bat` only undoes what `register.bat`
     itself set, not a choice made through Windows' own UI.
2. **If "Suppress plugin folder" was ever turned on**, open the picker and
   uncheck it first, so Designer's `Plugins` / `Assets` folders get moved
   back from their `-suppressed` siblings before the app is removed —
   otherwise they're left stuck renamed with no app around to restore them.
3. **Delete `settings.json`**, if you want to remove every trace of the
   app's saved preferences. It's either next to the EXE, or under
   `%AppData%\QDSVersionLauncher\settings.json` — see *Settings file*
   below for which applies to you.
4. **Delete the EXE** (and `app.ico` / `qsys.ico`, if kept alongside). For
   a portable, installer-less app like this, that's the entire footprint —
   nothing else needs cleaning up.

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
list, per-project remembered versions, which projects are pinned to skip
the picker, the most-recently-used list, the "Suppress plugin folder"
checkbox state, and the "Show picker on" mode — feel free to hand-edit it
if needed.

- **Stale per-project entries are cleaned up automatically.** The
  per-project remembered-version and pinned-project lists would otherwise
  grow forever as projects get renamed, moved, or deleted. Right before the
  picker is shown (never on the fast, no-dialog launch path, so this can't
  add latency there), the launcher checks — at most once every 7 days —
  whether each remembered or pinned project file still exists, and drops
  the entries whose file is gone. Entries on a network path
  (`\\server\share\...`) are left alone rather than checked, since an
  unreachable share could otherwise make the check hang.

## Versioning

The picker's bottom-right corner shows the app's current version (e.g.
`ver1.0.1`), read straight from the `<Version>` property in
`QDSVersionLauncher.csproj` — there's no separate place to update it.
Bump that one property before publishing a new build and the label updates
itself; a legacy `qdv.ico` from older copies is still recognized as a
fallback icon, but `app.ico` is what ships now.

## Known limitations

- Whichever modifier-key mode you pick, the detection reads live keyboard
  state the instant the process starts, since Explorer doesn't pass
  modifier keys to launched programs. In practice this works fine for a
  normal double-click, but if you release the key unusually fast it could
  occasionally be missed. (This doesn't apply to the **Always** mode,
  which doesn't look at the keyboard at all.)
- **If you pick a Shift-based mode ("Shift is held" or "Ctrl + Shift are
  held"), be aware Shift isn't fully reliable — Ctrl is.** Windows Explorer
  itself reserves Shift+click for extending a file selection, so a
  Shift-held double-click can get consumed by Explorer's own
  range-selection handling before it ever reaches this app, regardless of
  what the app's code checks for. Ctrl doesn't have that conflict and
  reliably forces the picker, which is why it's the default.
- Designed and tested for the common case of Designer installed under the
  default QSC folders; very unusual custom install setups may need a
  manual **Add folder...** the first time.


### Direct Registration via PowerShell
If you want to register the executable directly after building without using `register.bat`, run:

```powershell
& "$env:USERPROFILE\Desktop\QDSVersionLauncher\bin\Release\net8.0-windows\QDS Version Launcher.exe" --register-association
```

## License & Credits
- **Author / Concept:** Created by Shogo Hori (assisted by AI collaboration).
- **License:** Distributed under the [MIT License](LICENSE).

This tool was generated and refined with AI assistance.
It is provided "as is" without warranty of any kind.
Please test and verify in your environment before using it in production.