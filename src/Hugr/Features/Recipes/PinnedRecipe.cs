// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Hugr.Configuration;
using UnityEngine;

namespace Hugr.Features.Recipes
{
    /// <summary>
    /// The pinned recipes, stored in the BepInEx config — the single source of persistence — as
    /// item prefab names in the order they were pinned, each with the number of crafts aimed for
    /// when that is more than one (<c>ArrowIron:5</c>), and resolved back through
    /// <c>ObjectDB</c> on demand.
    /// </summary>
    internal static class PinnedRecipe
    {
        private const char Separator = ',';

        private const char QuantityMark = ':';

        private static readonly List<Pin> Resolved = new List<Pin>();

        /// <summary>Config value <see cref="Resolved"/> was read from, null until every name resolves.</summary>
        private static string _resolvedFrom;

        internal enum PinResult
        {
            Pinned,
            Unpinned,
            Full
        }

        /// <summary>
        /// Pins the recipe, or unpins it when it was already pinned. A new pin is refused once
        /// <see cref="ModConfig.MaxPinnedRecipes"/> recipes are pinned.
        /// </summary>
        internal static PinResult Toggle(Recipe recipe)
        {
            List<KeyValuePair<string, int>> pins = Read();
            string name = recipe.m_item.gameObject.name;
            bool unpinned = pins.RemoveAll(pin => pin.Key == name) > 0;
            if (!unpinned)
            {
                if (pins.Count >= ModConfig.MaxPinnedRecipes.Value)
                {
                    return PinResult.Full;
                }

                pins.Add(new KeyValuePair<string, int>(name, 1));
            }

            Write(pins);
            return unpinned ? PinResult.Unpinned : PinResult.Pinned;
        }

        /// <summary>
        /// Moves the number of crafts aimed for by <paramref name="delta"/>, never below one,
        /// pinning the recipe first when it was not. Returns the new number, or zero when the
        /// recipe could not be pinned because the limit is reached.
        /// </summary>
        internal static int AddQuantity(Recipe recipe, int delta)
        {
            List<KeyValuePair<string, int>> pins = Read();
            string name = recipe.m_item.gameObject.name;
            int index = pins.FindIndex(pin => pin.Key == name);
            int quantity = 1;
            if (index >= 0)
            {
                quantity = Math.Max(1, pins[index].Value + delta);
                pins[index] = new KeyValuePair<string, int>(name, quantity);
            }
            else if (pins.Count >= ModConfig.MaxPinnedRecipes.Value)
            {
                return 0;
            }
            else
            {
                pins.Add(new KeyValuePair<string, int>(name, quantity));
            }

            Write(pins);
            return quantity;
        }

        /// <summary>
        /// The pinned recipes the game knows, in pin order; a name it does not know is skipped and
        /// looked up again on the next call. The list is shared: read it, do not keep it.
        /// </summary>
        internal static List<Pin> ResolveAll()
        {
            string value = ModConfig.PinnedRecipe.Value;
            if (ObjectDB.instance == null)
            {
                Resolved.Clear();
                _resolvedFrom = null;
                return Resolved;
            }

            if (value == _resolvedFrom)
            {
                return Resolved;
            }

            Resolved.Clear();
            bool complete = true;
            foreach (KeyValuePair<string, int> pin in Read())
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(pin.Key);
                ItemDrop item = prefab == null ? null : prefab.GetComponent<ItemDrop>();
                Recipe recipe = item == null ? null : ObjectDB.instance.GetRecipe(item.m_itemData);
                if (recipe == null)
                {
                    complete = false;
                }
                else
                {
                    Resolved.Add(new Pin(recipe, pin.Value));
                }
            }

            _resolvedFrom = complete ? value : null;
            return Resolved;
        }

        /// <summary>Names and quantities as the config holds them; a missing or unreadable quantity is one.</summary>
        private static List<KeyValuePair<string, int>> Read()
        {
            List<KeyValuePair<string, int>> pins = new List<KeyValuePair<string, int>>();
            foreach (string entry in ModConfig.PinnedRecipe.Value.Split(Separator))
            {
                string[] parts = entry.Split(QuantityMark);
                string name = parts[0].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                int quantity = 1;
                if (parts.Length > 1 && int.TryParse(parts[1], out int parsed) && parsed > 1)
                {
                    quantity = parsed;
                }

                pins.Add(new KeyValuePair<string, int>(name, quantity));
            }

            return pins;
        }

        private static void Write(List<KeyValuePair<string, int>> pins)
        {
            ModConfig.PinnedRecipe.Value = string.Join(
                Separator.ToString(),
                pins.Select(pin => pin.Value > 1 ? pin.Key + QuantityMark + pin.Value : pin.Key));
        }

        /// <summary>A pinned recipe and how many crafts of it are aimed for.</summary>
        internal readonly struct Pin
        {
            internal Pin(Recipe recipe, int quantity)
            {
                Recipe = recipe;
                Quantity = quantity;
            }

            internal Recipe Recipe { get; }

            internal int Quantity { get; }
        }
    }
}
