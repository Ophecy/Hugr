// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Utilities;
using UnityEngine;

namespace Hugr.Features.Slots
{
    /// <summary>
    /// A key press over an item of the player inventory destroys that stack, instead of dropping
    /// it and leaving it to despawn. By default the key has to be pressed twice on the same item
    /// within a few seconds. Equipped items and items in a locked slot are refused.
    /// Execution: client. Persistence: the player inventory itself. Server interaction: none —
    /// the item never enters the world.
    /// </summary>
    internal static class Trash
    {
        /// <summary>How long the first press waits for the second.</summary>
        private const float ConfirmWindow = 3f;

        private static ItemDrop.ItemData _asked;

        private static float _askedAt;

        internal static void Bind(Harmony harmony)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.Trash,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.Update)),
                AccessTools.Method(typeof(Trash), nameof(OnUpdate)));
        }

        private static void OnUpdate(InventoryGui __instance)
        {
            try
            {
                if (!ModConfig.TrashKey.Value.IsDown() || !InputGate.TakesInput())
                {
                    return;
                }

                ItemDrop.ItemData item = InventoryHover.Item(__instance, out bool ofPlayer);
                if (item == null || !ofPlayer)
                {
                    return;
                }

                Player player = Player.m_localPlayer;
                string name = Localization.instance.Localize(item.m_shared.m_name);
                if (player.IsItemEquiped(item))
                {
                    Say(player, "Unequip " + name + " first");
                }
                else if (FavoriteSlots.IsFavorite(item.m_gridPos))
                {
                    Say(player, "This slot is locked");
                }
                else if (ModConfig.TrashConfirm.Value && (item != _asked || Time.time > _askedAt + ConfirmWindow))
                {
                    _asked = item;
                    _askedAt = Time.time;
                    Say(player, "Press again to destroy " + name + " x" + item.m_stack);
                }
                else
                {
                    _asked = null;
                    int count = item.m_stack;
                    player.GetInventory().RemoveItem(item);
                    Say(player, "Destroyed " + name + " x" + count);
                }
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                Plugin.Report(ErrorCodes.TrashUnexpected, "unexpected failure while destroying an item", exception);
            }
        }

        private static void Say(Player player, string message)
        {
            player.Message(MessageHud.MessageType.Center, message, 0, null, false);
        }
    }
}
