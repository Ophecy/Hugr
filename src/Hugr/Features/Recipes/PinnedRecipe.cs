// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using Hugr.Configuration;
using UnityEngine;

namespace Hugr.Features.Recipes
{
    /// <summary>
    /// The pinned recipe, stored as the item prefab name in the BepInEx config — the single
    /// source of persistence — and resolved back through <c>ObjectDB</c> on demand.
    /// </summary>
    internal static class PinnedRecipe
    {
        private static string _resolvedName;
        private static Recipe _resolved;

        internal static bool IsPinned(Recipe recipe)
        {
            return recipe != null && NameOf(recipe) == ModConfig.PinnedRecipe.Value;
        }

        /// <summary>Pins the recipe, or clears the pin when it was already the pinned one.</summary>
        internal static bool Toggle(Recipe recipe)
        {
            bool pin = !IsPinned(recipe);
            ModConfig.PinnedRecipe.Value = pin ? NameOf(recipe) : string.Empty;
            return pin;
        }

        /// <summary>The pinned recipe, or null when nothing is pinned or the item is unknown.</summary>
        internal static Recipe Resolve()
        {
            string name = ModConfig.PinnedRecipe.Value;
            if (string.IsNullOrEmpty(name) || ObjectDB.instance == null)
            {
                return null;
            }

            if (name == _resolvedName && _resolved != null)
            {
                return _resolved;
            }

            _resolvedName = name;
            _resolved = null;

            GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
            ItemDrop item = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            if (item != null)
            {
                _resolved = ObjectDB.instance.GetRecipe(item.m_itemData);
            }

            return _resolved;
        }

        private static string NameOf(Recipe recipe)
        {
            return recipe == null || recipe.m_item == null ? string.Empty : recipe.m_item.gameObject.name;
        }
    }
}
