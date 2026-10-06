// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Reflection;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Utilities;

namespace Hugr.Features.Recipes
{
    /// <summary>
    /// Pins, on a key press, the recipe of the item under the pointer in the inventory or else
    /// the one selected in the crafting panel, and keeps the on-screen tracker attached to the
    /// HUD. Two more keys raise and lower how many crafts of that recipe are aimed for. Several
    /// recipes can be pinned, up to a configurable limit. The pins are a config entry, so they
    /// survive the session like any other setting.
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

            bool pin = ModConfig.PinRecipeKey.Value.IsDown();
            int delta = ModConfig.PinMoreKey.Value.IsDown() ? 1 : ModConfig.PinLessKey.Value.IsDown() ? -1 : 0;
            if ((!pin && delta == 0) || !InventoryGui.IsVisible() || !InputGate.TakesInput())
            {
                return;
            }

            // An item under the pointer comes first: it is what the player is looking at.
            ItemDrop.ItemData hovered = InventoryHover.Item(gui, out _);
            Recipe recipe = hovered != null
                ? ObjectDB.instance.GetRecipe(hovered)
                : _recipeOfPair.GetValue(_selectedRecipe.GetValue(gui), null) as Recipe;
            if (recipe == null || recipe.m_item == null)
            {
                return;
            }

            string name = Localization.instance.Localize(recipe.m_item.m_itemData.m_shared.m_name);
            string full = "Pin limit reached (" + ModConfig.MaxPinnedRecipes.Value + ")";
            string message;
            if (pin)
            {
                switch (PinnedRecipe.Toggle(recipe))
                {
                    case PinnedRecipe.PinResult.Pinned:
                        message = "Pinned " + name;
                        break;
                    case PinnedRecipe.PinResult.Unpinned:
                        message = "Unpinned " + name;
                        break;
                    default:
                        message = full;
                        break;
                }
            }
            else
            {
                int quantity = PinnedRecipe.AddQuantity(recipe, delta);
                message = quantity > 0 ? "Pinned " + name + " x" + quantity * recipe.m_amount : full;
            }

            Player.m_localPlayer.Message(MessageHud.MessageType.Center, message, 0, null, false);
        }
    }
}
