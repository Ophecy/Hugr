// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Features.Crafting;
using UnityEngine;

namespace Hugr.Features.Smelting
{
    /// <summary>
    /// Shift+E on the ore or fuel input of a smelting station loads it to its capacity in one
    /// press. Each unit goes through the vanilla <c>Smelter.OnAddOre</c> / <c>OnAddFuel</c>, so
    /// the item choice, the messages, the effects and the RPC to the station's owner are the
    /// game's own. With crafting from the nearby chests on, what the inventory lacks is first
    /// moved into it from those chests, then loaded the same way.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: the vanilla
    /// RPC_AddOre / RPC_AddFuel, one per unit, as when the player holds E.
    /// </summary>
    internal static class SmelterFill
    {
        internal static void Bind(Harmony harmony)
        {
            MethodInfo interact = AccessTools.Method(typeof(Switch), nameof(Switch.Interact));
            if (interact == null)
            {
                throw new HugrException("HUGR-FILL-001", "Switch no longer exposes Interact.");
            }

            FeatureSwitch.Bind(
                harmony,
                ModConfig.SmelterFill,
                interact,
                AccessTools.Method(typeof(SmelterFill), nameof(OnInteract)),
                prefix: true);
        }

        private static bool OnInteract(Switch __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (!alt || hold || __instance.m_onUse == null)
            {
                return true;
            }

            try
            {
                Smelter smelter = __instance.GetComponentInParent<Smelter>();
                int free = smelter == null ? 0 : FreeSlots(smelter, __instance);
                if (free <= 0)
                {
                    // Not a smelter input, or a full one: vanilla answers, with its own message.
                    return true;
                }

                // The owner applies an addition without checking the capacity, and a station owned
                // by another client reports its level late: the count is fixed before the burst.
                Switch input = __instance;
                List<string> accepted = Accepted(smelter, input);
                int added = Add(input, character, Math.Min(free, Carried(character, accepted)));
                int lack = free - added;
                bool fetching = lack > 0
                    && ContainerResources.Fetch(accepted, lack, () => Resume(smelter, input, accepted, lack));
                if (added == 0 && !fetching)
                {
                    // Nothing to load, here or in the chests: vanilla says so.
                    return true;
                }

                Plugin.Trace("Smelter fill: " + added + "/" + free + " added to " + smelter.m_name + ".");
                input.m_lastUseTime = Time.time;
                __result = true;
                return false;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("HUGR-FILL-000: unexpected failure while filling a smelter (" + exception + ").");
                return true;
            }
        }

        /// <summary>The chests answered: loads what they handed over, up to what was lacking.</summary>
        private static void Resume(Smelter smelter, Switch input, List<string> accepted, int lack)
        {
            try
            {
                Player player = Player.m_localPlayer;
                if (player == null || smelter == null || input == null || input.m_onUse == null)
                {
                    return;
                }

                int count = Math.Min(Math.Min(lack, FreeSlots(smelter, input)), Carried(player, accepted));
                int added = Add(input, player, count);
                Plugin.Trace("Smelter fill: " + added + "/" + lack + " more added to " + smelter.m_name + " from the chests.");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("HUGR-FILL-002: could not load what the chests handed over (" + exception + ").");
            }
        }

        /// <summary>Presses the input up to <paramref name="count"/> times, until vanilla refuses.</summary>
        private static int Add(Switch input, Humanoid character, int count)
        {
            int added = 0;
            while (added < count && input.m_onUse(input, character, null))
            {
                added++;
            }

            return added;
        }

        /// <summary>Units the targeted input still takes, by the limits vanilla checks itself.</summary>
        private static int FreeSlots(Smelter smelter, Switch input)
        {
            if (input == smelter.m_addOreSwitch)
            {
                return smelter.m_maxOre - smelter.GetQueueSize();
            }

            if (input == smelter.m_addWoodSwitch)
            {
                return (int)(smelter.m_maxFuel - smelter.GetFuel());
            }

            return 0;
        }

        /// <summary>Names of the items the input takes, in the order vanilla looks for them.</summary>
        private static List<string> Accepted(Smelter smelter, Switch input)
        {
            if (input == smelter.m_addWoodSwitch)
            {
                return new List<string> { smelter.m_fuelItem.m_itemData.m_shared.m_name };
            }

            return smelter.m_conversion
                .Where(conversion => conversion.m_from != null)
                .Select(conversion => conversion.m_from.m_itemData.m_shared.m_name)
                .Distinct()
                .ToList();
        }

        private static int Carried(Humanoid character, List<string> accepted)
        {
            Inventory inventory = character.GetInventory();
            return accepted.Sum(name => inventory.CountItems(name));
        }
    }
}
