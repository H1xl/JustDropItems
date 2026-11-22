# Development log

History of JustDropItems before its first public release. During development the builds were numbered
1.0.0; none of them was published, so they are listed here as development stages `dev-N`.
Published versions are in [package/CHANGELOG.md](../package/CHANGELOG.md).

Development names: Scavenger's Trade, then ItemShare.

## dev-2 — 2026-11-17 (built as 1.0.0)
Fixes from a code review:
- Equipment is no longer lost at stage end when the slot is filled: it drops at the owner's feet on the next stage.
- Gamepad: a button already held when an icon gets selected needs a release before it acts.
- Rerolled equipment picked up by swapping keeps its Recycler mark; the swapped-out equipment gets its own mark.
- Recycler and printed marks are lowered when a stack shrinks, so they never stick to fresh copies.
- Temporary copies: whole copies move first, the partly decayed one last, so a gift no longer merges away a copy.
- Gamepad: Drop all no longer confirms a gift in give mode.
- A gift to a player who left mid-flight drops on the floor where they were last seen, with its marks.
- The pickup message counts partly decayed temporary copies.
- With a host without the mod, clicks on own icons open the inspect panel as in the base game.
- The "host has no mod" chat line shows again in every run.
- The amount window is rebuilt only when what it shows changes.
- Internal: built through the code shared by H1xl's mods (logging, hooks, the Risk Of Options page, the item
  flight); a "ready." line in the log at startup.
- Internal (2026-11-18): game members reached by name are listed in `plugin/GameMembers.cs` and checked by a test
  against the game's assemblies after each game update.
- Fixed (2026-11-18): turning the wheel down with a big step (Ctrl, Alt) across two kinds of copies stopped at the farther border instead of the nearer one.
- Internal (2026-11-18): the stack split, the wheel step and host clamp, the mark counts and the item/equipment rule decisions moved to `plugin/Logic/` (no game calls) and are covered by unit tests (`tests/unit`, `dotnet test`).
- Internal (2026-11-18): automated in-game scenarios (not in this repository): 27 cases over drop amounts, kinds and order, host rules, equipment, Recycler and printed marks, Substandard Duplicator, Artifact of Command and the AutoLootDistribution API. No mod change.
- Internal (2026-11-21): player docs reviewed (game names checked against the language files, Reporting problems).
- Internal (2026-11-21, optimization pass): gamepad window hints built once per button/language change instead of every frame, one shared recipient list; the blacklist is resolved only when it changes (was: every rules setting change) and each catalog name is normalized once; unused `SharedLocked` token and `Settings.IsBlacklisted` removed; drop point helper `ItemFlight.DropPoint` (`_shared`).

## dev-1 — 2026-11-15 (built as 1.0.0)
- First complete build.
- Renamed from Scavenger's Trade (the development name) to Just Drop Items: the package, the plugin GUID (`com.H1xl.JustDropItems`) and the config file (`com.H1xl.JustDropItems.cfg`) all use the new name.
