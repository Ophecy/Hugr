// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Reflection;
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
        // InventoryGui.m_selectedRecipe is a private nested struct, so it has no name C# can
        // spell: it is read through its own Recipe property instead, once per key press.
        private static FieldInfo _selectedRecipe;
        private static PropertyInfo _recipeOfPair;

        internal static void Bind(Harmony harmony)
        {
            _selectedRecipe = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe")
                ?? throw new HugrException(
                    ErrorCodes.RecipeSelectionMissing, "InventoryGui no longer exposes the selected recipe.");

            _recipeOfPair = AccessTools.Property(_selectedRecipe.FieldType, "Recipe")
                ?? throw new HugrException(
                    ErrorCodes.RecipeOfSelectionMissing, "The selected recipe no longer carries a Recipe.");

            FeatureSwitch.Bind(
                harmony,
                ModConfig.RecipeTracker,
                AccessTools.Method(typeof(InventoryGui), "Update"),
                AccessTools.Method(typeof(RecipePinning), nameof(OnUpdate)));
        }

        private static void OnUpdate(InventoryGui __instance)
        {
            try
            {
                Pin(__instance);
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                Plugin.Report(ErrorCodes.RecipeUnexpected, "unexpected failure while pinning", exception);
            }
        }

        private static void Pin(InventoryGui gui)
        {
            RecipeTrackerHud.Ensure();

            if (!InventoryGui.IsVisible() || !ModConfig.PinRecipeKey.Value.IsDown())
            {
                return;
            }

            Recipe recipe = _recipeOfPair.GetValue(_selectedRecipe.GetValue(gui), null) as Recipe;
            if (recipe == null || recipe.m_item == null || Player.m_localPlayer == null)
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
