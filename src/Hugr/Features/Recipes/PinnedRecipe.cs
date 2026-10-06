// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Hugr.Configuration;
using UnityEngine;

namespace Hugr.Features.Recipes
{
    /// <summary>
    /// The pinned recipes, stored as item prefab names in the BepInEx config — the single source
    /// of persistence — in the order they were pinned, and resolved back through
    /// <c>ObjectDB</c> on demand.
    /// </summary>
    internal static class PinnedRecipe
    {
        private const char Separator = ',';

        private static readonly List<Recipe> Resolved = new List<Recipe>();

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
            List<string> names = Names();
            string name = recipe.m_item.gameObject.name;
            bool unpinned = names.Remove(name);
            if (!unpinned)
            {
                if (names.Count >= ModConfig.MaxPinnedRecipes.Value)
                {
                    return PinResult.Full;
                }

                names.Add(name);
            }

            ModConfig.PinnedRecipe.Value = string.Join(Separator.ToString(), names);
            return unpinned ? PinResult.Unpinned : PinResult.Pinned;
        }

        /// <summary>
        /// The pinned recipes the game knows, in pin order; a name it does not know is skipped and
        /// looked up again on the next call. The list is shared: read it, do not keep it.
        /// </summary>
        internal static List<Recipe> ResolveAll()
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
            foreach (string name in Names())
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
                ItemDrop item = prefab == null ? null : prefab.GetComponent<ItemDrop>();
                Recipe recipe = item == null ? null : ObjectDB.instance.GetRecipe(item.m_itemData);
                if (recipe == null)
                {
                    complete = false;
                }
                else
                {
                    Resolved.Add(recipe);
                }
            }

            _resolvedFrom = complete ? value : null;
            return Resolved;
        }

        private static List<string> Names()
        {
            List<string> names = new List<string>();
            foreach (string name in ModConfig.PinnedRecipe.Value.Split(Separator))
            {
                if (name.Trim().Length > 0)
                {
                    names.Add(name.Trim());
                }
            }

            return names;
        }
    }
}
