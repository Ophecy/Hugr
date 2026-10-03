// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using HarmonyLib;
using Hugr.Configuration;
using UnityEngine;

namespace Hugr.Features.Sorting
{
    /// <summary>
    /// On a key press, every chest in range takes from the inventory the items it already holds,
    /// through the vanilla "stack all" of the chest's hover text: once
    /// <see cref="ContainerClaims"/> has the chest handed over, the vanilla
    /// <c>Inventory.StackAll</c> runs here — equipped items stay, stacks are topped up first — and
    /// one message totals the whole batch.
    /// Execution: client. Persistence: the chests themselves, through the vanilla save.
    /// Server interaction: vanilla RPCs only, through <see cref="ContainerClaims"/>.
    /// </summary>
    internal static class QuickStack
    {
        private static bool _running;

        private static int _batch;

        /// <summary>Chests of the batch still to answer, plus one for the request loop itself.</summary>
        private static int _waiting;

        private static int _stored;

        internal static void Bind(Harmony harmony)
        {
            ModConfig.QuickStack.SettingChanged += (sender, args) => _running = false;

            FeatureSwitch.Bind(
                harmony,
                ModConfig.QuickStack,
                AccessTools.Method(typeof(InventoryGui), "Update"),
                AccessTools.Method(typeof(QuickStack), nameof(OnUpdate)));
        }

        private static void OnUpdate()
        {
            try
            {
                ContainerClaims.Tick();
                if (!_running && ModConfig.QuickStackKey.Value.IsDown() && TakesInput())
                {
                    Request(Player.m_localPlayer);
                }
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                Plugin.Report(ErrorCodes.StackUnexpected, "unexpected failure while stacking", exception);
            }
        }

        /// <summary>
        /// The gate vanilla puts on the inventory key, so typing never triggers a stack. The
        /// postfix also runs when <c>InventoryGui.Update</c> bails out early on a dead or
        /// teleporting player, hence the checks on the player.
        /// </summary>
        private static bool TakesInput()
        {
            Player player = Player.m_localPlayer;
            return player != null
                && !player.IsDead()
                && !player.IsTeleporting()
                && (Chat.instance == null || !Chat.instance.HasFocus())
                && (Hud.instance == null || !Hud.instance.m_buildUi.SearchFieldFocused)
                && !Console.IsVisible()
                && !TextInput.IsVisible()
                && !Menu.IsVisible()
                && !Minimap.IsOpen();
        }

        /// <summary>
        /// Asks every chest in range that already holds a carried item. A chest this client owns
        /// stacks within the call, hence the loop counting itself among the answers to wait for.
        /// </summary>
        private static void Request(Player player)
        {
            HashSet<string> carried = new HashSet<string>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (!player.IsItemEquiped(item))
                {
                    carried.Add(item.m_shared.m_name);
                }
            }

            int batch = ++_batch;
            _running = true;
            _stored = 0;
            _waiting = 1;

            List<Container> nearby = ContainerClaims.Nearby(
                new[] { player.transform.position }, ModConfig.QuickStackRange.Value);
            foreach (Container container in nearby)
            {
                if (Holds(container, carried))
                {
                    _waiting++;
                    ContainerClaims.Request(container, owned => Store(batch, owned), () => Done(batch));
                }
            }

            Done(batch);
        }

        private static void Store(int batch, Container container)
        {
            Player player = Player.m_localPlayer;
            if (batch == _batch && player != null)
            {
                int stored = container.GetInventory().StackAll(player.GetInventory());
                if (stored > 0)
                {
                    _stored += stored;
                    InventoryGui.instance.m_moveItemEffects.Create(container.transform.position, Quaternion.identity);
                }
            }

            Done(batch);
        }

        /// <summary>One chest answered, or the request loop ended: the last one totals the batch.</summary>
        private static void Done(int batch)
        {
            if (batch != _batch || --_waiting > 0)
            {
                return;
            }

            _running = false;
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    _stored > 0 ? "$msg_stackall " + _stored : "$msg_stackall_none");
            }
        }

        /// <summary>A chest takes part only if it already holds one of the carried items.</summary>
        private static bool Holds(Container container, HashSet<string> carried)
        {
            foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
            {
                if (carried.Contains(item.m_shared.m_name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
