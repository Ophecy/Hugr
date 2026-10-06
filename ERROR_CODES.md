# Hugr error codes

Every failure Hugr logs to `BepInEx/LogOutput.log` carries a `HUGR-<DOMAIN>-<NNN>` code: the
domain names the feature, the number the step that failed. `000` is usually the unexpected
failure caught at the feature's boundary — the log line then ends with the full exception.

A failure never takes the game down: the feature that failed is skipped or stopped, the others
keep running. Most codes ending in "missing" or "unreachable" mean a Valheim update moved
something Hugr relies on: report the code with the game version.

The codes are declared in [`src/Hugr/ErrorCodes.cs`](src/Hugr/ErrorCodes.cs), one constant per
code. A retired number is never reused.

## PATCH — hooking the game

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-PATCH-000` | Error | A feature could not be bound at startup, for an unexpected reason. | That feature is off for the session. |
| `HUGR-PATCH-001` | Error | The game method a feature hooks no longer exists. The message names the setting. | That feature is off for the session. |

## UI — the Hugr tab in the settings panel

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-UI-000` | Error | Unexpected failure while adding the tab. | No Hugr tab; the settings panel is untouched. |
| `HUGR-UI-001` | Error | The settings panel has no tab handler. | No Hugr tab. |
| `HUGR-UI-002` | Error | The settings panel exposes no tab to clone. | No Hugr tab. |
| `HUGR-UI-005` | Error | The template tab has no button to clone. | No Hugr tab. |
| `HUGR-UI-006` | Error | The cloned tab button has no label. | No Hugr tab. |
| `HUGR-UI-007` | Error | The cloned row has no toggle. | No Hugr tab. |
| `HUGR-UI-008` | Error | The cloned row has no caption. | No Hugr tab. |
| `HUGR-UI-009` | Error | No vanilla toggle was found to use as a row template. | No Hugr tab. |
| `HUGR-UI-010` | Error | A field of the settings panel is not reachable. The message names it. | No Hugr tab. |
| `HUGR-UI-011` | Error | The row template is the page itself: nothing to clone. | No Hugr tab. |
| `HUGR-UI-012` | Warning | The gamepad hints of the tab bar are unreachable. | The tab works; it may show a leftover gamepad hint. |
| `HUGR-UI-013` | Error | The settings were not saved when OK was pressed. | The toggles keep their previous values. |
| `HUGR-UI-014` | Error | The settings were not loaded into the tab. | The toggles may not reflect the config file. |
| `HUGR-UI-015` | Error | The row template has no caption to style the version line with. | No Hugr tab. |
| `HUGR-UI-016` | Error | The cloned page lost its row template. | No Hugr tab. |
| `HUGR-UI-017` | Warning | The settings panel has no scroll view to take the wheel speed from. | The tab works but does not scroll: the last settings may sit below its bottom edge. |

`HUGR-UI-003` and `HUGR-UI-004` are retired.

## REPAIR — automatic repair, repair everything

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-REPAIR-000` | Error | Unexpected failure after the repair button was pressed. | The vanilla press repaired one item; the rest is left. |
| `HUGR-REPAIR-001` | Error | The game no longer exposes its repair routine. | Both repair features are off for the session. |
| `HUGR-REPAIR-002` | Error | A repair pass did not converge and was stopped after 256 items. | The pass ends; what was repaired stays repaired. |
| `HUGR-REPAIR-003` | Error | Unexpected failure during the automatic repair. | The panel opens, nothing more is repaired. |

## RECIPE — pinned recipe and its HUD

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-RECIPE-000` | Error | Unexpected failure while pinning or unpinning. | The pin is unchanged. |
| `HUGR-RECIPE-001` | Error | The crafting panel no longer exposes its selected recipe. | Pinning is off for the session. |
| `HUGR-RECIPE-002` | Error | The requirement widget has no name text to clone the title from. | The tracker is stopped. |
| `HUGR-RECIPE-003` | Error | A tracker row has no icon. | The tracker is stopped. |
| `HUGR-RECIPE-004` | Error | A tracker row has no amount text. | The tracker is stopped. |
| `HUGR-RECIPE-005` | Error | The selected recipe no longer carries a recipe. | Pinning is off for the session. |
| `HUGR-RECIPE-006` | Error | Unexpected failure in the tracker. | The tracker is stopped until the plugin reloads. |
| `HUGR-RECIPE-007` | Error | A tracker row has no name text. | The tracker is stopped. |

## SORT — sort buttons

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-SORT-000` | Error | Unexpected failure while building the sort buttons. | No sort button. |
| `HUGR-SORT-001` | Error | The inventory no longer exposes its change notification. | Sorting is off for the session. |
| `HUGR-SORT-002` | Error | The open container is not reachable from the inventory panel. | Sorting is off for the session. |
| `HUGR-SORT-003` | Error | The container panel has no button to clone. | No sort button. |
| `HUGR-SORT-004` | Error | The cloned button has no label. | No sort button. |
| `HUGR-SORT-005` | Error | A sort failed when the button was pressed. | That inventory is left as it was at the failure. |
| `HUGR-SORT-006` | Error | The panel has no weight badge to line the button up with. | No sort button. |

## STACK — quick stack to nearby chests

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-STACK-000` | Error | Unexpected failure while stacking. | That quick stack is abandoned. |

## CLAIM — taking a chest over before changing its content

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-CLAIM-001` | Warning | A chest did not answer, or did not hand over its content, within 5 s. | That chest is left untouched. |

## CRAFT — craft and build from nearby chests

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-CRAFT-000` | Error | Unexpected failure while waiting for the chests. | The pull in flight may not complete. |
| `HUGR-CRAFT-001` | Warning | The chests did not hand over everything that was missing: inventory full, or a chest did not answer. | Vanilla then says what is still missing. |
| `HUGR-CRAFT-002` | Error | The resources of a craft could not be fetched. | The craft runs on the inventory alone. |
| `HUGR-CRAFT-003` | Error | The resources of a piece could not be fetched. | The placement runs on the inventory alone. |

## SMELT — fill a smelter with Shift+E

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-SMELT-000` | Error | Unexpected failure while filling a smelter. | Shift+E falls back to the vanilla single addition. |
| `HUGR-SMELT-001` | Error | The game no longer exposes the switch interaction. | The feature is off for the session. |
| `HUGR-SMELT-002` | Error | What the chests handed over could not be loaded. | The items stay in the inventory. |

## SEARCH — inventory search

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-SEARCH-000` | Error | Unexpected failure in the search. | The search field is removed until the plugin reloads. |
| `HUGR-SEARCH-001` | Error | The build menu has no search field to clone. | No search field. |
| `HUGR-SEARCH-002` | Error | The cloned search field is not an input field. | No search field. |

## FILTER — inventory category filters

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-FILTER-000` | Error | Unexpected failure in the category filters. | The filter buttons are removed until the plugin reloads. |
| `HUGR-FILTER-001` | Error | The container panel has no button to clone. | No filter buttons. |
| `HUGR-FILTER-002` | Error | The cloned button has no label. | No filter buttons. |

## SERVER — remembered server passwords

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-SERVER-000` | Error | Unexpected failure while handling a server password. | The password prompt keeps its vanilla behaviour. |

## CLOCK and COMPASS — HUD

| Code | Level | Meaning | Effect |
| --- | --- | --- | --- |
| `HUGR-CLOCK-000` | Error | Unexpected failure in the clock. | The clock is removed. |
| `HUGR-COMPASS-000` | Error | Unexpected failure in the compass. | The compass is removed. |
