# Hugr

A client-side quality-of-life mod for Valheim: automatic repair at crafting stations,
pinned recipes with live resource tracking, and a global shopping list.

Hugr never has to be installed on the server, never touches the world save and adds no
gameplay content. See `Hugr_Specifications_Fonctionnelles.md` and
`Hugr_Specifications_Techniques.md` (French) for the full specification.

## Status

V1 — repair. The plugin loads, its tab lives in the vanilla settings panel, and both repair
features are in. The recipe toggles are still placeholders: they land with V2.

## Features

| Setting | What it does |
| --- | --- |
| Automatic repair | Repairs every item the station accepts as soon as the crafting panel opens. |
| Repair everything at once | One press of the vanilla repair button repairs the whole pile. |

Both call Valheim's own repair routine, so the station rules, the skill gain, the effects and
the messages are the vanilla ones — only the number of clicks changes. A feature you turn off
has no patch installed on the game at all.

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
7. Join a vanilla server with no Hugr installed — repeat step 5, it still works.

If the tab is missing, the log carries a `HUGR-UI-0xx` code naming the step that failed; a
repair that refuses to run logs a `HUGR-REPAIR-0xx` or `HUGR-PATCH-0xx` code the same way.

## License

Hugr is free software under the [GNU General Public License v3.0](LICENSE) or later.

It is an independent BepInEx plugin: it links against nothing it redistributes, ships no
game asset, and reads Valheim's assemblies only from your own installation.
