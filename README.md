# Hugr

A client-side quality-of-life mod for Valheim: automatic repair at crafting stations,
pinned recipes with live resource tracking, and a global shopping list.

Hugr never has to be installed on the server, never touches the world save and adds no
gameplay content. See `Hugr_Specifications_Fonctionnelles.md` and
`Hugr_Specifications_Techniques.md` (French) for the full specification.

## Status

V2 — recipe tracker. Repair is done, and a pinned recipe now follows you on screen with live
resource counts. Multiple pins and the shopping list come with V3.

## Features

| Setting | What it does |
| --- | --- |
| Automatic repair | Repairs every item the station accepts as soon as the crafting panel opens. |
| Repair everything at once | One press of the vanilla repair button repairs the whole pile. |
| Pinned recipes | Select a recipe in the crafting panel and press **P**: it stays on screen with what you have and what you still need. Press **P** again on it to unpin. |
| Shopping list | Placeholder, lands with V3. |
| Sort button | A **Sort** button on the inventory and on any open container: partial stacks are merged, then items are grouped by kind and name. The hotbar row keeps its slots — its stacks are still topped up. |
| Quick stack to nearby chests | Press **G**: every chest within 10 m takes from your inventory the items it already holds, through the chest's own vanilla "stack all". Equipped items stay; chests in use, someone else's private chests, chests behind someone else's ward and dungeon chests are skipped. Key and range are `QuickStackKey` and `QuickStackRange` in the config file. |
| Craft and build from nearby chests | Chests, carts and ships within 15 m of you, or of a crafting station whose range covers you, count in the crafting panel and the build menu. When you craft or place a piece, only what your inventory lacks is moved into it from those chests, then the game crafts or builds as usual. If a chest belongs to another player's client, the craft waits for it and the placement is replayed once the resources arrive. Range is `CraftFromContainersRange` in the config file. |
| Fill smelters with Shift+E | **Shift+E** on the ore or the fuel input of a smelter, kiln, blast furnace and the like loads it to its capacity in one press, from your inventory, with the items the game would pick itself. With **Craft and build from nearby chests** on, what your inventory lacks is fetched from those chests first. Plain **E** still adds one. |
| Remember server passwords | The first time you join a password-protected server, the password you type is kept once the server accepts it; next time Hugr types it for you. If the server refuses a remembered password, Hugr forgets it and joins again so you are asked. Passwords sit in plain text in `BepInEx/config/ophecy.Hugr.cfg`, under `[ServerPasswords]`. |
| Clock | The day and the time of day under the minimap, on the sun's scale: 06:00 is sunrise, 18:00 sunset. |
| Compass | A strip across the top of the screen with the eight directions, in the game's language. |
| Inventory search | A search field above the inventory: items of the inventory and of the open container that do not match are greyed out. The field empties when the inventory closes. |

Repair calls Valheim's own repair routine, so the station rules, the skill gain, the effects and
the messages are the vanilla ones — only the number of clicks changes. The tracker clones the
crafting panel's own requirement widgets, icons included. A feature you turn off has no patch
installed on the game at all.

The pin key is `PinRecipeKey` in the config file, and the pin itself is stored there too, so it
survives a restart.

Sorting a chest changes what that chest contains, exactly as moving items by hand does — the
same code path, the same save. Hugr writes nothing of its own into the world.

## Build

Requirements: Rider or Visual Studio 2022, the
[.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48),
and a local Valheim install with
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).

Hugr references the game and BepInEx assemblies directly from your install, so the
versions always match what you are modding. Nothing is downloaded, nothing is copied
into the repository. Point `VALHEIM_INSTALL` at the folder containing `valheim.exe`:

```
setx VALHEIM_INSTALL "E:\SteamLibrary\steamapps\common\Valheim"
```

Restart the IDE afterwards, then build `Hugr.sln` in **Debug**: `Hugr.dll` is compiled and
copied to `<VALHEIM_INSTALL>\BepInEx\plugins\Hugr\` in one step.

A Release build compiles without deploying. If `VALHEIM_INSTALL` is missing or wrong, the
build stops with a message naming the variable rather than a wall of missing-type errors.

### Hot reload

Restarting Valheim for every change is the slow way. Install
[ScriptEngine](https://thunderstore.io/c/valheim/p/ValheimModding/ScriptEngine/) — the BepInEx
team's reloader — and build with:

```
dotnet build src/Hugr/Hugr.csproj -c Debug -p:HotReload=true
```

`Hugr.dll` then lands in `BepInEx/scripts/` instead of `BepInEx/plugins/`, and **F6** in game
reloads it: patches are removed, the plugin is rebuilt from the new file and re-applies itself.
The build clears the other location on its way, because the same plugin GUID loaded twice is a
confusing failure.

What comes back on its own after a reload: the repair patches, the recipe tracker, and the
settings tab the next time the panel is opened. Reload with the settings panel closed — a panel
already on screen keeps the previous build's tab until it is closed and reopened.

## Usage

Open Valheim's **Settings** panel, from the main menu or in game: Hugr adds its own tab
next to the vanilla ones. **OK** saves, **Back** discards, exactly like the other tabs.

Settings are stored in `BepInEx/config/ophecy.Hugr.cfg` and can also be edited there.
Turn on **Debug mode** (`DebugMode`) to get `[debug]` lines in the BepInEx log — timings, chests
found, resources moved — when reporting a problem.

## Verifying V1

1. Launch Valheim, check the BepInEx log for `Hugr loaded.` then `Settings tab injected.`
2. Open Settings — a Hugr tab sits next to the vanilla ones, in the game's own style.
3. Flip the toggles, press OK, reopen — values hold. Flip them, press Back — values revert.
4. Quit, relaunch, reopen the tab — values still hold.
5. Damage a few pieces of gear, open a workbench — everything is repaired, the log says how many.
6. Turn **Automatic repair** off, damage gear again, open the workbench — nothing is repaired;
   one press of the repair button fixes the whole pile.
7. Open a crafting station, select a recipe, press **P** — it appears on the right of the screen
   with one line per resource, red while you are short.
8. Close everything and pick up one of those resources — the count climbs on its own.
9. Quit, relaunch — the recipe is still pinned. Press **P** on it again to unpin.
10. Fill a chest with scattered half-stacks, press **Sort** — they merge and group, and the
    message says how many slots that freed. Do the same on your inventory: row one is untouched.
11. Put some wood in a chest, walk away from it with more wood and some stone, press **G** within
    10 m — the wood goes into the chest, the stone stays with you.
12. Empty your inventory of wood into a chest near a workbench, then craft a club and place a
    wood wall — the wood is taken from the chest, only what each one costs.
13. Join a vanilla server with no Hugr installed — repeat steps 5, 7, 10, 11 and 12, they still
    work.

If the tab is missing, the log carries a `HUGR-UI-0xx` code naming the step that failed; a
repair or a tracker that refuses to run logs a `HUGR-REPAIR-0xx`, `HUGR-RECIPE-0xx` or
`HUGR-PATCH-0xx` code the same way.

## License

Hugr is free software under the [GNU General Public License v3.0](LICENSE) or later.

It is an independent BepInEx plugin: it links against nothing it redistributes, ships no
game asset, and reads Valheim's assemblies only from your own installation.
