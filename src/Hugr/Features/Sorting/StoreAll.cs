// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Features.Slots;
using Hugr.Utilities;
using UnityEngine;

namespace Hugr.Features.Sorting
{
    /// <summary>
    /// On a key press, empties the inventory into the chests in range: each item goes to a chest
    /// that already holds some of it, and what no chest holds goes to the first one with room.
    /// Equipped items, the hotbar row and the locked slots stay. Nothing moves until every chest
    /// has answered, so a slow chest that holds the item is not beaten by a fast empty one.
    /// Execution: client. Persistence: the chests themselves, through the vanilla save.
    /// Server interaction: vanilla RPCs only, through <see cref="ContainerClaims"/>.
    /// </summary>
    internal static class StoreAll
    {
        private static readonly List<Container> Owned = new List<Container>();

        private static bool _running;

        private static int _batch;

        /// <summary>Chests of the batch still to answer, plus one for the request loop itself.</summary>
        private static int _waiting;

        internal static void Bind(Harmony harmony)
        {
            ModConfig.StoreAll.SettingChanged += (sender, args) => _running = false;

            FeatureSwitch.Bind(
                harmony,
                ModConfig.StoreAll,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.Update)),
                AccessTools.Method(typeof(StoreAll), nameof(OnUpdate)));
        }

        private static void OnUpdate()
        {
            try
            {
                ContainerClaims.Tick();
                if (!_running && ModConfig.StoreAllKey.Value.IsDown() && InputGate.TakesInput())
                {
                    Request(Player.m_localPlayer);
                }
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                Plugin.Report(ErrorCodes.StoreUnexpected, "unexpected failure while storing", exception);
            }
        }

        /// <summary>
        /// Asks every chest in range. A chest this client owns answers within the call, hence the
        /// loop counting itself among the answers to wait for.
        /// </summary>
        private static void Request(Player player)
        {
            int batch = ++_batch;
            _running = true;
            _waiting = 1;
            Owned.Clear();

            List<Container> nearby = ContainerClaims.Nearby(
                new[] { player.transform.position }, ModConfig.StoreAllRange.Value);
            foreach (Container container in nearby)
            {
                _waiting++;
                ContainerClaims.Request(
                    container,
                    owned =>
                    {
                        if (batch == _batch)
                        {
                            Owned.Add(owned);
                        }

                        Done(batch);
                    },
                    () => Done(batch));
            }

            Done(batch);
        }

        /// <summary>One chest answered, or the request loop ended: the last one stores the inventory.</summary>
        private static void Done(int batch)
        {
            if (batch != _batch || --_waiting > 0)
            {
                return;
            }

            _running = false;
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            int stored = Store(player);
            player.Message(
                MessageHud.MessageType.Center, stored > 0 ? "$msg_stackall " + stored : "$msg_stackall_none");
        }

        private static int Store(Player player)
        {
            // A chest handed over early may have gone to another player while the others answered.
            Owned.RemoveAll(container => container == null || !container.IsOwner());

            Inventory inventory = player.GetInventory();
            HashSet<Container> filled = new HashSet<Container>();
            int stored = 0;
            foreach (bool holding in new[] { true, false })
            {
                foreach (ItemDrop.ItemData item in inventory.GetAllItems().ToArray())
                {
                    if (player.IsItemEquiped(item) || item.m_gridPos.y == 0 || FavoriteSlots.IsFavorite(item.m_gridPos))
                    {
                        continue;
                    }

                    foreach (Container container in Owned)
                    {
                        Inventory chest = container.GetInventory();
                        if ((holding && !chest.HaveItem(item.m_shared.m_name)) || !chest.CanAddItem(item))
                        {
                            continue;
                        }

                        int count = item.m_stack;
                        if (chest.AddItem(item.Clone()))
                        {
                            inventory.RemoveItem(item);
                            filled.Add(container);
                            stored += count;
                            break;
                        }
                    }
                }
            }

            foreach (Container container in filled)
            {
                InventoryGui.instance.m_moveItemEffects.Create(container.transform.position, Quaternion.identity);
            }

            return stored;
        }
    }
}
