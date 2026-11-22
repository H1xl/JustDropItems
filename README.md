# Just Drop Items

Drop items or hand them to a teammate who needs them more: a survivor-specific item, a Wandering CHEF ingredient,
or an item your void item would corrupt. **Temporary** and **Ethereal** copies stay what they are, and the Recycler
can't be abused to reroll the same item forever.

**Who needs the mod**: the host, and every player who wants to drop or give items. Players without it can still join
and see ordinary pickups and flying items.

A more detailed description of the mod, with screenshots and all settings, is on its [Thunderstore page](https://thunderstore.io/c/riskofrain2/p/H1xl/JustDropItems/).

- **Download:** [Thunderstore](https://thunderstore.io/c/riskofrain2/p/H1xl/JustDropItems/)
- **Features, controls and settings:** [package/README.md](package/README.md)
- **Changes:** [CHANGELOG](package/CHANGELOG.md) (released versions) · [DEVLOG](docs/DEVLOG.md) (development history)
- **Bugs and ideas:** [Issues](https://github.com/H1xl/JustDropItems/issues)

## Building
Requirements: .NET SDK 8 or newer, Risk of Rain 2, and a mod-manager profile (r2modman or Thunderstore Mod Manager)
with BepInEx and [Risk Of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) installed
(the plugin references it; for players it stays optional).

1. Copy `paths.local.props.example` to `paths.local.props` and set `GameDir` (the game folder), `ProfilesDir` (the
   mod manager's profiles folder) and `RefProfile` (the profile name).
2. `dotnet build plugin -c Release` → `plugin/bin/Release/JustDropItems.dll`.
3. Copy the dll into `<profile>/BepInEx/plugins/JustDropItems/`.

## Tests
`dotnet test tests/unit` — the mod's pure logic (`plugin/Logic/`: stack split, amount steps, Recycler marks, item
rules), no game needed.

## For other mods
A mod that gives items straight into an inventory, instead of letting the player pick them up, can call
`JustDropItems.Api.ItemGranted(master, item, recycled, duplicated)` on the host, by reflection (no hard dependency),
so the Recycler and printer marks follow the item. Details and the call: `plugin/Api.cs`.

## Repository layout
| Folder | Contents |
|---|---|
| `plugin/` | BepInEx plugin source (`netstandard2.1`) |
| `shared/` | code shared by H1xl's mods, compiled into the plugin |
| `package/` | Thunderstore files: manifest, README, CHANGELOG, icon |
| `tests/unit/` | unit tests |
| `docs/` | development log, README images |

## License
[MIT](LICENSE): the code. Not covered: the mod icon (art by MethadosBackup).

## Credits
- Mod icon art: **MethadosBackup** ([X / Twitter](https://x.com/MethadosBackup))

This is a free fan mod and is not affiliated with Hopoo Games / Gearbox.
