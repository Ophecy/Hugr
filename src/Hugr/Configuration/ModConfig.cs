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
        internal static ConfigEntry<bool> DebugMode { get; private set; }

        internal static ConfigEntry<bool> AutoRepair { get; private set; }

        internal static ConfigEntry<bool> RepairAll { get; private set; }

        internal static ConfigEntry<bool> RecipeTracker { get; private set; }

        internal static ConfigEntry<bool> ShoppingList { get; private set; }

        internal static ConfigEntry<bool> SortButton { get; private set; }

        internal static ConfigEntry<bool> ServerPasswords { get; private set; }

        internal static ConfigEntry<bool> Clock { get; private set; }

        internal static ConfigEntry<bool> Compass { get; private set; }

        internal static ConfigEntry<bool> InventorySearch { get; private set; }

        internal static ConfigEntry<bool> QuickStack { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> QuickStackKey { get; private set; }

        internal static ConfigEntry<float> QuickStackRange { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PinRecipeKey { get; private set; }

        /// <summary>Prefab name of the pinned recipe, written by the crafting panel.</summary>
        internal static ConfigEntry<string> PinnedRecipe { get; private set; }

        private static ConfigFile _config;

        /// <summary>
        /// Remembered password of a server, keyed by <c>ZNet.GetServerString(true)</c>; empty when
        /// none is known. Bound on first use, since the servers are only known at join time.
        /// </summary>
        internal static ConfigEntry<string> ServerPassword(string server)
        {
            return _config.Bind(
                "ServerPasswords", SanitizeKey(server), string.Empty,
                "Password of this server, stored in plain text. Empty the value to forget it.");
        }

        /// <summary>BepInEx refuses these characters in a key; IPv6 hosts carry brackets.</summary>
        private static string SanitizeKey(string key)
        {
            foreach (char invalid in new[] { '=', '\n', '\t', '\\', '"', '\'', '[', ']' })
            {
                key = key.Replace(invalid, '_');
            }

            return key.Trim();
        }

        internal static void Bind(ConfigFile config)
        {
            _config = config;

            DebugMode = config.Bind(
                "General", "DebugMode", false,
                "Write diagnostic lines, prefixed [debug], to the BepInEx log: timings, chests found, resources moved.");

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

            SortButton = config.Bind(
                "Inventory", "SortButton", true,
                "Add a Sort button to the inventory and to open containers.");

            ServerPasswords = config.Bind(
                "Servers", "ServerPasswords", true,
                "Remember the password of every server joined and type it in on the next join.");

            Clock = config.Bind(
                "Hud", "Clock", true,
                "Show the day and the time of day under the minimap.");

            Compass = config.Bind(
                "Hud", "Compass", true,
                "Show a compass strip at the top of the screen.");

            InventorySearch = config.Bind(
                "Inventory", "InventorySearch", true,
                "Add a search field that greys out the items not matching it, in the inventory and open containers.");

            QuickStack = config.Bind(
                "Inventory", "QuickStack", true,
                "On a key press, store in every chest in range the items that chest already holds.");

            QuickStackKey = config.Bind(
                "Inventory", "QuickStackKey", new KeyboardShortcut(KeyCode.G),
                "Stores the carried items into the nearby chests that already hold them.");

            QuickStackRange = config.Bind(
                "Inventory", "QuickStackRange", 10f,
                new ConfigDescription(
                    "Distance, in metres, within which chests take part in a quick stack.",
                    new AcceptableValueRange<float>(1f, 30f)));

            PinRecipeKey = config.Bind(
                "Recipes", "PinRecipeKey", new KeyboardShortcut(KeyCode.P),
                "Pins or unpins the recipe selected in the crafting panel.");

            PinnedRecipe = config.Bind(
                "Recipes", "PinnedRecipe", string.Empty,
                "Pinned recipe, by item prefab name. Pin from the crafting panel rather than here.");
        }
    }
}
