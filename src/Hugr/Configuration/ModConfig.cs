// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using BepInEx.Configuration;

namespace Hugr.Configuration
{
    /// <summary>
    /// Single source of persistence: every setting is a BepInEx <see cref="ConfigEntry{T}"/>
    /// stored in BepInEx/config/. The settings tab edits these entries, it never saves anything
    /// itself.
    /// </summary>
    /// <remarks>
    /// V0: the feature toggles carry no gameplay behaviour yet, they only prove the
    /// config round-trip. Each feature reads its own entry when it lands.
    /// </remarks>
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> AutoRepair { get; private set; }

        internal static ConfigEntry<bool> RecipeTracker { get; private set; }

        internal static ConfigEntry<bool> ShoppingList { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            AutoRepair = config.Bind(
                "Repair", "AutoRepair", true,
                "Repair every compatible item when a crafting station is opened.");

            RecipeTracker = config.Bind(
                "Recipes", "RecipeTracker", true,
                "Keep pinned recipes and their resources on screen.");

            ShoppingList = config.Bind(
                "Recipes", "ShoppingList", false,
                "Aggregate the resources required by every pinned recipe.");
        }
    }
}
