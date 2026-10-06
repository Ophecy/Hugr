// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Utilities;
using TMPro;

namespace Hugr.Features.Slots
{
    /// <summary>
    /// Locks slots of the player inventory against Hugr's own bulk actions: what sits in a locked
    /// slot is left alone by the sort, by "store all" and by the trash key. A key press over a
    /// slot locks or unlocks it, and a locked slot shows a star in its corner. The lock belongs
    /// to the slot, not to the item: nothing is written on the item, so nothing reaches the
    /// character or the world save.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: none.
    /// </summary>
    internal static class FavoriteSlots
    {
        private const string Marker = "*";

        private static readonly HashSet<Vector2i> Slots = new HashSet<Vector2i>();

        /// <summary>Config value <see cref="Slots"/> was read from.</summary>
        private static string _read;

        /// <summary>What the corner captions were last written for; the grid rebuilds its slots on a resize.</summary>
        private static string _markedFor;

        private static InventoryElement _markedFirst;

        private static int _markedCount;

        internal static void Bind(Harmony harmony)
        {
            ModConfig.Favorites.SettingChanged += (sender, args) =>
            {
                if (!ModConfig.Favorites.Value)
                {
                    Remove();
                }
            };

            FeatureSwitch.Bind(
                harmony,
                ModConfig.Favorites,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.Update)),
                AccessTools.Method(typeof(FavoriteSlots), nameof(OnUpdate)));

            FeatureSwitch.Bind(
                harmony,
                ModConfig.Favorites,
                AccessTools.Method(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui)),
                AccessTools.Method(typeof(FavoriteSlots), nameof(OnUpdateGui)));
        }

        /// <summary>Whether the slot of the player inventory is locked; never with the feature off.</summary>
        internal static bool IsFavorite(Vector2i slot)
        {
            return ModConfig.Favorites.Value && Read().Contains(slot);
        }

        /// <summary>Gives the slots their vanilla corner captions back when the feature goes off or unloads.</summary>
        internal static void Remove()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui != null && gui.m_playerGrid != null)
            {
                Mark(gui.m_playerGrid.m_elements, new HashSet<Vector2i>());
            }

            _markedFor = null;
        }

        private static void OnUpdate(InventoryGui __instance)
        {
            try
            {
                if (!ModConfig.FavoriteKey.Value.IsDown() || !InputGate.TakesInput()
                    || !InventoryHover.TryGetSlot(__instance, out _, out Vector2i slot, out bool ofPlayer)
                    || !ofPlayer)
                {
                    return;
                }

                HashSet<Vector2i> slots = Read();
                bool locked = slots.Add(slot);
                if (!locked)
                {
                    slots.Remove(slot);
                }

                List<string> entries = new List<string>();
                foreach (Vector2i entry in slots)
                {
                    entries.Add(entry.x + ":" + entry.y);
                }

                ModConfig.FavoriteSlots.Value = string.Join(",", entries);
                Player.m_localPlayer.Message(
                    MessageHud.MessageType.TopLeft, locked ? "Slot locked" : "Slot unlocked", 0, null, false);
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                Plugin.Report(ErrorCodes.FavoriteUnexpected, "unexpected failure while locking a slot", exception);
            }
        }

        /// <summary>Runs every frame the grid is drawn; the captions are only rewritten on a change.</summary>
        private static void OnUpdateGui(InventoryGrid __instance)
        {
            try
            {
                InventoryGui gui = InventoryGui.instance;
                List<InventoryElement> elements = __instance.m_elements;
                if (gui == null || __instance != gui.m_playerGrid || elements.Count == 0)
                {
                    return;
                }

                string slots = ModConfig.FavoriteSlots.Value;
                if (slots == _markedFor && elements[0] == _markedFirst && elements.Count == _markedCount)
                {
                    return;
                }

                // Written first: a grid that cannot be marked is reported once, not every frame.
                _markedFor = slots;
                _markedFirst = elements[0];
                _markedCount = elements.Count;
                Mark(elements, Read());
            }
            catch (Exception exception)
            {
                Plugin.Report(ErrorCodes.FavoriteMarkFailed, "the locked slots could not be marked", exception);
            }
        }

        /// <summary>
        /// Writes the corner caption of every slot: vanilla numbers the hotbar there and leaves it
        /// off elsewhere, a locked slot adds a star to that.
        /// </summary>
        private static void Mark(List<InventoryElement> elements, HashSet<Vector2i> slots)
        {
            foreach (InventoryElement element in elements)
            {
                UnityEngine.Transform corner = element.transform.Find("binding");
                TMP_Text caption = corner == null ? null : corner.GetComponent<TMP_Text>();
                if (caption == null)
                {
                    throw new HugrException(
                        ErrorCodes.FavoriteCaptionMissing, "An inventory slot no longer carries its corner caption.");
                }

                Vector2i slot = element.Position;
                bool hotbar = slot.y == 0;
                bool locked = slots.Contains(slot);
                caption.text = (hotbar ? (slot.x + 1).ToString() : string.Empty) + (locked ? Marker : string.Empty);
                caption.enabled = hotbar || locked;
            }
        }

        private static HashSet<Vector2i> Read()
        {
            string value = ModConfig.FavoriteSlots.Value;
            if (value == _read)
            {
                return Slots;
            }

            Slots.Clear();
            foreach (string entry in value.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    Slots.Add(new Vector2i(x, y));
                }
            }

            _read = value;
            return Slots;
        }
    }
}
