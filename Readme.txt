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

## ⚠️ Read this first: association order matters

Run `register.bat` *before* touching Windows' own "Open with" / Default
apps UI for `.qsys`. Doing it the other way around is the #1 cause of
`.qsys` files permanently showing this app's own icon (`app.ico`) instead
of the intended `qsys.ico`, even after `register.bat` is run afterward.

Here's why: if you associate `.qsys` with this launcher through Windows
Settings -> Default apps (or Explorer's "Open with" -> "Look for another
app on this PC" -> browsing to the `.exe` directly) before `register.bat`
has ever run, Windows creates its own separate `Applications\<exe>`
registry entry for the raw `.exe` instead of using the ProgID this app
registers. That entry has no custom icon, so it falls back to the app's
own icon -- permanently, since re-running `register.bat` later updates
the *other* (correct) registry entry, not the one Windows is actually
using once that choice has been made.

Correct order:
1. Run `register.bat` first. On its own this is enough -- double-clicking
   a `.qsys` file will already open it through this launcher, with the
   correct `qsys.ico`, no further setup needed.
2. If Windows still shows "How do you want to open this file?" (e.g. the
   very first time you double-click one), pick **QDS Version Launcher**
   from the list shown. Don't use "Look for another app on this PC" to
   browse to the `.exe` -- that's what creates the broken entry above.

Already stuck with the wrong icon? Reset the association first: Settings
-> Apps -> Default apps, search `.qsys`, reset/remove the current choice,
then repeat the two steps above.

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
  dialog. The picker itself has a **"Show picker on:"** section where you
  choose what skips that shortcut and forces the picker open: **Always**,
  **Ctrl is held**, **Shift is held**, or **Ctrl + Shift are held**. The
  choice is saved the moment you pick it — no separate save step. Default
  is **Ctrl**, and it's the one to rely on — see Known limitations below
  for why Shift can be inconsistent.
- **Pin a project to always skip the picker.** Independent of the
  **Show picker on:** mode above, the picker also has an "Always use this
  version for this project" checkbox. Check it and click Open, and that
  exact project will always launch straight into the selected version
  from then on — even if the mode is set to **Always**. Holding Ctrl or
  Shift on a later launch still forces the picker open for that project,
  so the pin can be changed or removed.
- **Portable.** Builds to a single, self-contained `.exe` — no installer,
  no separate .NET runtime to install, safe to run from a USB stick.
- **Manage extra scan folders.** The **Manage Folders...** button opens a
  small dialog listing every custom folder you've added, with **Add...**
  and **Remove** — so a folder added by mistake (or one that no longer
  applies) can be taken back out again, instead of only ever growing the
  list.
- **Suppress plugin folder.** A checkbox in the picker that temporarily
  moves Designer's `Plugins` and `Assets` folders out of the way (a quick
  "safe mode" for ruling out a misbehaving plugin). It's a persistent
  on/off toggle saved in `settings.json`, not tied to one launch: checking
  it moves the folders right away, unchecking moves them back right away.
  Designer must be fully closed to toggle it.
- **One-click Clean Registry button.** Does the same thing as
  `unregister.bat` — un-associates `.qsys` files from this launcher,
  restoring whatever handled them before (if anything) — but reachable
  straight from the picker window, so there's no need to go find the
  batch file before deleting or uninstalling the app.
- **Shows its own version number.** The bottom-right corner of the picker
  displays something like `ver1.0.1`, so it's easy to tell at a glance —
  or when reporting an issue — exactly which build is running.

## Installing

1. Put `QDSVersionLauncher.exe` (and `app.ico`, if you kept it alongside)
   wherever you want it to live long-term. `qsys.ico` is optional -- include
   it too if you want `.qsys` files themselves to show a distinct icon in
   Explorer, separate from the app's own icon.
2. Run `register.bat` once. This makes `.qsys` files open through this
   launcher instead of a single fixed Designer version. It only touches
   your own user registry hive (`HKEY_CURRENT_USER`), so it doesn't need
   administrator rights and doesn't affect other Windows accounts on the
   same PC. Do this before touching Windows' own "Open with" / Default
   apps UI for `.qsys` -- see the warning near the top of this file for
   why the order matters.
3. Double-click any `.qsys` file -- the picker should appear.

## Uninstalling

This app has no installer and never appears in "Programs and Features" --
everything it touches lives in your own user registry hive or in its own
files, so removing it is just a few steps:

1. Undo the file association first, while the EXE still exists. Click
   Clean Registry inside the picker, or run `unregister.bat` (both do the
   same thing). This restores whatever `.qsys` was associated with before
   this app (if anything) and clears the stale "Recommended apps" cache
   entry Windows sometimes keeps for the exe.
   - If you also set `.qsys`'s default app through Windows' own Settings ->
     Default apps UI at some point (see the warning near the top), reset
     that separately -- `unregister.bat` only undoes what `register.bat`
     itself set, not a choice made through Windows' own UI.
2. If "Suppress plugin folder" was ever turned on, open the picker and
   uncheck it first, so Designer's `Plugins` / `Assets` folders get moved
   back from their `-suppressed` siblings before the app is removed --
   otherwise they're left stuck renamed with no app around to restore them.
3. Delete `settings.json`, if you want to remove every trace of the app's
   saved preferences. It's either next to the EXE, or under
   `%AppData%\QDSVersionLauncher\settings.json` -- see "Settings file"
   below for which applies to you.
4. Delete the EXE (and `app.ico` / `qsys.ico`, if kept alongside). For a
   portable, installer-less app like this, that's the entire footprint --
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

Stale per-project entries are cleaned up automatically: right before the
picker is shown (never on the fast, no-dialog launch path), the launcher
checks — at most once every 7 days — whether each remembered or pinned
project file still exists, and drops the entries whose file is gone.
Entries on a network path are left alone rather than checked, since an
unreachable share could otherwise make the check hang.

## Versioning

The picker's bottom-right corner shows the app's current version (e.g.
`ver1.0.1`), read straight from the `<Version>` property in the .csproj
file — there's no separate place to update it. Bump that one property
before publishing a new build and the label updates itself automatically.

## Known limitations

- Shift is offered as a mode alternative to Ctrl for forcing the picker,
  but isn't reliable — Windows Explorer reserves Shift+click for extending
  a file selection, so it can be consumed by Explorer before this app ever
  sees it. Use Ctrl.
- Whichever modifier-key mode you pick, detection reads live key state the
  instant the process starts, so releasing the key unusually fast could
  occasionally be missed. (Doesn't apply to the Always mode.)