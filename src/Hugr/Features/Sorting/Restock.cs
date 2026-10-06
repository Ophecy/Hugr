// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Features.Crafting;
using Hugr.Features.Slots;
using Hugr.Utilities;

namespace Hugr.Features.Sorting
{
    /// <summary>
    /// On a key press, tops up from the nearby chests the stacks the player chose to carry: those
    /// of the hotbar row and of the locked slots. Each one is completed to a full stack, never
    /// more, so the chests are not emptied into the inventory. The items come through
    /// <see cref="ContainerResources.Fetch"/>, which needs crafting from the nearby chests on.
    /// Execution: client. Persistence: the chests and the inventory themselves, through the
    /// vanilla save. Server interaction: vanilla RPCs only, as crafting from the chests.
    /// </summary>
    internal static class Restock
    {
        private static bool _running;

        internal static void Bind(Harmony harmony)
        {
            ModConfig.Restock.SettingChanged += (sender, args) => _running = false;

            FeatureSwitch.Bind(
                harmony,
                ModConfig.Restock,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.Update)),
                AccessTools.Method(typeof(Restock), nameof(OnUpdate)));
        }

        private static void OnUpdate()
        {
            try
            {
                ContainerClaims.Tick();
                if (_running || !ModConfig.RestockKey.Value.IsDown() || !InputGate.TakesInput())
                {
                    return;
                }

                Player player = Player.m_localPlayer;
                List<KeyValuePair<string, int>> wanted = Wanted(player);
                int before = Carried(player, wanted);
                _running = true;
                Next(wanted, 0, before);
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                _running = false;
                Plugin.Report(ErrorCodes.RestockUnexpected, "unexpected failure while restocking", exception);
            }
        }

        /// <summary>What each kept stack lacks to be full, added up by item.</summary>
        private static List<KeyValuePair<string, int>> Wanted(Player player)
        {
            List<KeyValuePair<string, int>> wanted = new List<KeyValuePair<string, int>>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                int lack = item.m_shared.m_maxStackSize - item.m_stack;
                if (lack <= 0 || (item.m_gridPos.y != 0 && !FavoriteSlots.IsFavorite(item.m_gridPos)))
                {
                    continue;
                }

                string name = item.m_shared.m_name;
                int index = wanted.FindIndex(entry => entry.Key == name);
                if (index < 0)
                {
                    wanted.Add(new KeyValuePair<string, int>(name, lack));
                }
                else
                {
                    wanted[index] = new KeyValuePair<string, int>(name, wanted[index].Value + lack);
                }
            }

            return wanted;
        }

        /// <summary>
        /// Fetches one item after the other: a fetch only starts once the previous one is over,
        /// and an item the chests do not hold is skipped. The last one reports the total.
        /// </summary>
        private static void Next(List<KeyValuePair<string, int>> wanted, int index, int before)
        {
            try
            {
                for (; index < wanted.Count; index++)
                {
                    int next = index + 1;
                    if (ContainerResources.Fetch(
                            new[] { wanted[index].Key }, wanted[index].Value, () => Next(wanted, next, before)))
                    {
                        return;
                    }
                }

                _running = false;
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    int added = Carried(player, wanted) - before;
                    player.Message(
                        MessageHud.MessageType.Center,
                        added > 0 ? "Restocked " + added + " item(s)" : "Nothing to restock from the nearby chests",
                        0,
                        null,
                        false);
                }
            }
            catch (Exception exception)
            {
                _running = false;
                Plugin.Report(ErrorCodes.RestockFetchFailed, "the restock was interrupted", exception);
            }
        }

        private static int Carried(Player player, List<KeyValuePair<string, int>> wanted)
        {
            int carried = 0;
            foreach (KeyValuePair<string, int> entry in wanted)
            {
                carried += player.GetInventory().CountItems(entry.Key);
            }

            return carried;
        }
    }
}
