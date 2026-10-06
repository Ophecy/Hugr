// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using HarmonyLib;
using Hugr.Configuration;

namespace Hugr.Features.Search
{
    /// <summary>
    /// The inventory search field also narrows the crafting panel: recipes whose item does not
    /// match what is typed leave the list, in the craft tab and in the upgrade tab alike. The
    /// field is <see cref="InventorySearch"/>'s own — with that feature off there is nothing to
    /// type in, and the list stays whole. The list is filtered where vanilla builds it, so the
    /// selection, the sorting and the requirement checks are the game's.
    /// Execution: client. Persistence: none. Server interaction: none.
    /// </summary>
    internal static class RecipeSearch
    {
        internal static void Bind(Harmony harmony)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.RecipeSearch,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipeList)),
                AccessTools.Method(typeof(RecipeSearch), nameof(OnUpdateRecipeList)),
                prefix: true);

            ModConfig.RecipeSearch.SettingChanged += (sender, args) => Refresh();
            InventorySearch.TermChanged += Refresh;
        }

        /// <summary>
        /// Runs before vanilla turns the recipes into rows. The list is the panel's scratch list,
        /// refilled from the player's known recipes on every rebuild: trimming it loses nothing.
        /// </summary>
        private static void OnUpdateRecipeList(List<Recipe> recipes)
        {
            try
            {
                string term = InventorySearch.Term;
                if (term.Length > 0)
                {
                    recipes.RemoveAll(recipe => !InventorySearch.Matches(recipe.m_item.m_itemData, term));
                }
            }
            catch (Exception exception)
            {
                Plugin.Report(ErrorCodes.RecipeSearchUnexpected, "the recipe list was not filtered", exception);
            }
        }

        /// <summary>Rebuilds the list of the open crafting panel: vanilla only does so on its own events.</summary>
        private static void Refresh()
        {
            try
            {
                InventoryGui gui = InventoryGui.instance;
                if (gui != null && InventoryGui.IsVisible() && Player.m_localPlayer != null)
                {
                    gui.UpdateCraftingPanel();
                }
            }
            catch (Exception exception)
            {
                Plugin.Report(ErrorCodes.RecipeSearchRefreshFailed, "the recipe list was not rebuilt", exception);
            }
        }
    }
}
