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

Repair calls Valheim's own repair routine, so the station rules, the skill gain, the effects and
the messages are the vanilla ones — only the number of clicks changes. The tracker clones the
crafting panel's own requirement widgets, icons included. A feature you turn off has no patch
installed on the game at all.

The pin key is `PinRecipeKey` in the config file, and the pin itself is stored there too, so it
survives a restart.

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

Settings are stored in `BepInEx/config/com.ophecy.hugr.cfg` and can also be edited there.

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
10. Join a vanilla server with no Hugr installed — repeat steps 5 and 7, they still work.

If the tab is missing, the log carries a `HUGR-UI-0xx` code naming the step that failed; a
repair or a tracker that refuses to run logs a `HUGR-REPAIR-0xx`, `HUGR-RECIPE-0xx` or
`HUGR-PATCH-0xx` code the same way.

## License

Hugr is free software under the [GNU General Public License v3.0](LICENSE) or later.

It is an independent BepInEx plugin: it links against nothing it redistributes, ships no
game asset, and reads Valheim's assemblies only from your own installation.
