// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;

namespace Hugr.Features.Repair
{
    /// <summary>
    /// Turns the vanilla repair button into a repair-everything button: the game repairs the
    /// first item, Hugr finishes the pile. No extra widget is added, so the action stays where
    /// the player already looks for it and keeps its vanilla keyboard and gamepad navigation.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: none.
    /// </summary>
    internal static class RepairAll
    {
        internal static void Bind(Harmony harmony)
        {
            RepairService.Verify();

            FeatureSwitch.Bind(
                harmony,
                ModConfig.RepairAll,
                AccessTools.Method(typeof(InventoryGui), "OnRepairPressed"),
                AccessTools.Method(typeof(RepairAll), nameof(OnRepairPressed)));
        }

        private static void OnRepairPressed(InventoryGui __instance)
        {
            try
            {
                int repaired = RepairService.RepairEverything(__instance);
                if (repaired > 0)
                {
                    Plugin.Log.LogInfo("Repaired " + repaired + " more item(s).");
                }
            }
            catch (Exception exception)
            {
                // The vanilla press already repaired one item, so the button keeps working even
                // when the rest of the pass is refused.
                Plugin.Report(ErrorCodes.RepairUnexpected, "unexpected failure during repair", exception);
            }
        }
    }
}
