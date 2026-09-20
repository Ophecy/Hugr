// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using BepInEx.Configuration;
using UnityEngine;

namespace Hugr.Configuration
{
    /// <summary>
    /// Single source of persistence: every setting is a BepInEx <see cref="ConfigEntry{T}"/>
    /// stored in BepInEx/config/. The settings tab edits these entries, it never saves anything
    /// itself.
    /// </summary>
    /// <remarks>
    /// A toggle is the feature's switch, not a flag it reads: turning one off removes its
    /// Harmony patch. The recipe entries carry no behaviour yet, they land with V2.
    /// </remarks>
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> AutoRepair { get; private set; }

        internal static ConfigEntry<bool> RepairAll { get; private set; }

        internal static ConfigEntry<bool> RecipeTracker { get; private set; }

        internal static ConfigEntry<bool> ShoppingList { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PinRecipeKey { get; private set; }

        /// <summary>Prefab name of the pinned recipe, written by the crafting panel.</summary>
        internal static ConfigEntry<string> PinnedRecipe { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            AutoRepair = config.Bind(
                "Repair", "AutoRepair", true,
                "Repair every compatible item when a crafting station is opened.");

            RepairAll = config.Bind(
                "Repair", "RepairAll", true,
                "Repair every compatible item in a single press of the repair button.");

            RecipeTracker = config.Bind(
                "Recipes", "RecipeTracker", true,
                "Keep pinned recipes and their resources on screen.");

            ShoppingList = config.Bind(
                "Recipes", "ShoppingList", false,
                "Aggregate the resources required by every pinned recipe.");

            PinRecipeKey = config.Bind(
                "Recipes", "PinRecipeKey", new KeyboardShortcut(KeyCode.P),
                "Pins or unpins the recipe selected in the crafting panel.");

            PinnedRecipe = config.Bind(
                "Recipes", "PinnedRecipe", string.Empty,
                "Pinned recipe, by item prefab name. Pin from the crafting panel rather than here.");
        }
    }
}
