// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;

namespace Hugr.Features.Recipes
{
    /// <summary>
    /// Pins the recipe selected in the crafting panel, on a key press, and keeps the on-screen
    /// tracker attached to the HUD. The pin itself is a config entry, so it survives the session
    /// like any other setting.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: none — the
    /// recipe list and the inventory are already on the client.
    /// </summary>
    internal static class RecipePinning
    {
        private static AccessTools.FieldRef<InventoryGui, InventoryGui.RecipeDataPair> _selectedRecipe;

        internal static void Bind(Harmony harmony)
        {
            try
            {
                _selectedRecipe =
                    AccessTools.FieldRefAccess<InventoryGui, InventoryGui.RecipeDataPair>("m_selectedRecipe");
            }
            catch (Exception exception)
            {
                throw new HugrException(
                    "HUGR-RECIPE-001",
                    "InventoryGui.m_selectedRecipe is not reachable (" + exception.Message + ").");
            }

            FeatureSwitch.Bind(
                harmony,
                ModConfig.RecipeTracker,
                AccessTools.Method(typeof(InventoryGui), "Update"),
                AccessTools.Method(typeof(RecipePinning), nameof(OnUpdate)));
        }

        private static void OnUpdate(InventoryGui __instance)
        {
            RecipeTrackerHud.Ensure();

            if (!InventoryGui.IsVisible() || !ModConfig.PinRecipeKey.Value.IsDown())
            {
                return;
            }

            Recipe recipe = _selectedRecipe(__instance).Recipe;
            if (recipe == null || Player.m_localPlayer == null)
            {
                return;
            }

            bool pinned = PinnedRecipe.Toggle(recipe);
            Player.m_localPlayer.Message(
                MessageHud.MessageType.Center,
                (pinned ? "Pinned " : "Unpinned ") + Localization.instance.Localize(
                    recipe.m_item.m_itemData.m_shared.m_name),
                0,
                null,
                false);
        }
    }
}
