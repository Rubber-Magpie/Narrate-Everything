# Handoff notes

Where this fork stood when work moved from a Claude cloud session to Claude Code on the
maintainer's own computer (October 2026). Read `AGENTS.md` first for the project rules.

## The repository

- Working branch: `navigation-fixes` (the testers' build). `main` is the original
  ObjectInSpace mod, kept for comparison.
- Remotes: `origin` = https://github.com/rubber-magpie/Date-Everything-Access (private, empty
  until the first push), `upstream` = https://github.com/ObjectInSpace/date-everything-access.
- First push: `git push -u origin navigation-fixes` first (so it becomes the default branch),
  then `git push origin main`. Only push when the maintainer asks.
- Before pushing, keep checking that no game code or game assets go in (no files from the
  game folder, no decompiled code). The navigation data under `artifacts/navigation` is
  measurements the original author derived from the house layout; it is already public in the
  upstream repository and the mod needs `navigable_region.bake.json` to autowalk.

## Building and testing

- `Build-and-Install.bat`: builds and installs into the Steam copy of the game, and copies
  `prism.dll` next to `Date Everything.exe`. It refuses to run while the game is open. Check
  that the game is closed before launching it again, because Steam shows an error if it's
  already running.
- `Restore-Original-Mod.bat` puts back the original v1.2 DLL from `backup\`.
- `Make-Tester-Installer.bat` builds `DateEverythingAccess-Setup.exe` for testers from the
  installed copy in the game folder (`tester-package\build-setup.ps1`).
- Game log: `C:\Program Files (x86)\Steam\steamapps\common\Date Everything\BepInEx\LogOutput.log`.
  F9 turns on debug mode, which also logs everything spoken (`[SR]` lines).

## Working with the maintainer

- The maintainer uses NVDA. Tell them what changed in plain words; keep changes
  scoped to what was asked.
- Player characters are named by each player. In readmes and change logs say "your
  character" or "the player", never a particular name or pronoun.
- The mod must not override the game's rules (for example, bulletin board photos only when the
  game shows them).

## Recent work (newest first)

- Speech goes through Prism (`lib/prism`, v0.18.2) instead of Tolk. It follows NVDA
  starting and stopping mid-game (Prism's availability watch), and the Windows voice takes over
  when no screen reader is running.
- Mod tutorial (`AccessibilityWatcher.ModTutorial.cs`, texts `modtut_*` in `Loc.cs`): spoken
  steps alongside the game's tutorial; H repeats the latest. Work screen tips come from
  `ParallelChat.chatEnded` (Sam's first chat; then five finished chats in the customer list,
  `ChatMaster.CanopyChats`). The bed tip follows meeting Betty; the morning tip a change of day.
- Doors: stand-back-and-open keeps clear of the door's whole swing (live hinge and leaf from the
  door's collider), which fixed leaving the office in the tutorial.
- Turning: the auto-walk turn gain adapts when the game turns slower than expected (CrossOver).
- One key press no longer counts twice (Alt+O sometimes also opened the objects list).
- At startup the log gets an `[INPUTMAP] Keyboard summary` of the key bindings, for
  remote testers whose keys misbehave.

## Open items

- A CrossOver tester reported Z and X not working for interact/examine; the new keyboard
  summary in their log should show why.
