// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;

namespace Hugr.Features.Repair
{
    /// <summary>
    /// Repairs everything the station can repair as soon as the crafting panel opens, so the
    /// player never has to click the repair button once per item.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: none —
    /// durability lives in the player's own inventory and costs nothing to restore.
    /// </summary>
    internal static class AutoRepair
    {
        internal static void Bind(Harmony harmony)
        {
            RepairService.Verify();

            FeatureSwitch.Bind(
                harmony,
                ModConfig.AutoRepair,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.Show)),
                AccessTools.Method(typeof(AutoRepair), nameof(OnShow)));
        }

        /// <summary>
        /// Runs after the panel is up. Away from a station the vanilla guards make this a no-op,
        /// which is why opening a plain container or the inventory costs a single check.
        /// </summary>
        private static void OnShow(InventoryGui __instance)
        {
            try
            {
                int repaired = RepairService.RepairEverything(__instance);
                if (repaired > 0)
                {
                    Plugin.Log.LogInfo("Auto-repaired " + repaired + " item(s).");
                }
            }
            catch (Exception exception)
            {
                // The inventory must open whatever happens, so the failure is reported and the
                // panel is left alone.
                Plugin.Report(ErrorCodes.AutoRepairUnexpected, "unexpected failure during auto-repair", exception);
            }
        }
    }
}
