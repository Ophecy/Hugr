// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using HarmonyLib;

namespace Hugr.Features.Repair
{
    /// <summary>
    /// Drives Valheim's own repair routine until there is nothing left to repair. Hugr repairs
    /// nothing itself: it calls <c>InventoryGui.RepairOneItem</c>, which already applies the
    /// station rules, the skill gain, the effects and the message exactly like a button press.
    /// </summary>
    internal static class RepairService
    {
        /// <summary>
        /// Upper bound on one pass. Every repaired item leaves <c>Inventory.GetWornItems</c>, so
        /// the loop always converges; the cap only turns a future regression into an error code
        /// instead of a frozen game.
        /// </summary>
        private const int MaxItemsPerPass = 256;

        private static readonly MethodInfo HaveRepairableItems =
            AccessTools.Method(typeof(InventoryGui), "HaveRepairableItems");

        private static readonly MethodInfo RepairOneItem =
            AccessTools.Method(typeof(InventoryGui), "RepairOneItem");

        /// <summary>Checked once at startup, before any patch is installed.</summary>
        internal static void Verify()
        {
            if (HaveRepairableItems == null || RepairOneItem == null)
            {
                throw new HugrException(
                    "HUGR-REPAIR-001", "InventoryGui no longer exposes the vanilla repair routine.");
            }
        }

        /// <summary>
        /// Repairs every worn item the current crafting station accepts and returns how many were
        /// repaired. Returns 0 when the player is not at a station: the vanilla guards answer that.
        /// </summary>
        internal static int RepairEverything(InventoryGui gui)
        {
            int repaired = 0;

            while ((bool)HaveRepairableItems.Invoke(gui, null))
            {
                RepairOneItem.Invoke(gui, null);

                if (++repaired > MaxItemsPerPass)
                {
                    throw new HugrException(
                        "HUGR-REPAIR-002",
                        "Repair pass did not converge, stopped after " + MaxItemsPerPass + " items.");
                }
            }

            return repaired;
        }
    }
}
