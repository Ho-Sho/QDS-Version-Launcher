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
  version. Shift is checked too, but **Ctrl is the one to rely on** —
  Windows Explorer itself uses Shift+click for extending a file selection,
  so a Shift-held double-click can get eaten by that before it ever reaches
  this app.
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

Stale per-project entries are cleaned up automatically: right before the
picker is shown (never on the fast, no-dialog launch path), the launcher
checks — at most once every 7 days — whether each remembered project file
still exists, and drops the entries whose file is gone. Entries on a
network path are left alone rather than checked, since an unreachable share
could otherwise make the check hang.

## Known limitations

- Shift is checked as an alternative to Ctrl for forcing the picker, but
  isn't reliable — Windows Explorer reserves Shift+click for extending a
  file selection, so it can be consumed by Explorer before this app ever
  sees it. Use Ctrl.
- The keyboard-state detection reads live key state the instant the process
  starts, so releasing the key unusually fast could occasionally be missed.