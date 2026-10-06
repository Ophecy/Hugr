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
    /// Harmony patch.
    /// </remarks>
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> DebugMode { get; private set; }

        internal static ConfigEntry<bool> AutoRepair { get; private set; }

        internal static ConfigEntry<bool> RepairAll { get; private set; }

        internal static ConfigEntry<bool> RecipeTracker { get; private set; }

        internal static ConfigEntry<bool> ShoppingList { get; private set; }

        internal static ConfigEntry<bool> SortButton { get; private set; }

        internal static ConfigEntry<bool> SortBaseSlotsOnly { get; private set; }

        internal static ConfigEntry<bool> ServerPasswords { get; private set; }

        internal static ConfigEntry<bool> Clock { get; private set; }

        internal static ConfigEntry<bool> Compass { get; private set; }

        internal static ConfigEntry<bool> InventorySearch { get; private set; }

        internal static ConfigEntry<bool> CategoryFilters { get; private set; }

        internal static ConfigEntry<bool> RecipeSearch { get; private set; }

        internal static ConfigEntry<bool> SmelterFill { get; private set; }

        internal static ConfigEntry<bool> QuickStack { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> QuickStackKey { get; private set; }

        internal static ConfigEntry<float> QuickStackRange { get; private set; }

        internal static ConfigEntry<bool> StoreAll { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> StoreAllKey { get; private set; }

        internal static ConfigEntry<float> StoreAllRange { get; private set; }

        internal static ConfigEntry<bool> Restock { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> RestockKey { get; private set; }

        internal static ConfigEntry<bool> Favorites { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> FavoriteKey { get; private set; }

        /// <summary>Locked slots of the player inventory, as <c>x:y</c> pairs, written by the lock key.</summary>
        internal static ConfigEntry<string> FavoriteSlots { get; private set; }

        internal static ConfigEntry<bool> Trash { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> TrashKey { get; private set; }

        internal static ConfigEntry<bool> TrashConfirm { get; private set; }

        internal static ConfigEntry<bool> CraftFromContainers { get; private set; }

        internal static ConfigEntry<float> CraftFromContainersRange { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PinRecipeKey { get; private set; }

        /// <summary>Prefab names of the pinned recipes, comma-separated, written by the crafting panel.</summary>
        internal static ConfigEntry<string> PinnedRecipe { get; private set; }

        internal static ConfigEntry<int> MaxPinnedRecipes { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PinMoreKey { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PinLessKey { get; private set; }

        internal static ConfigEntry<bool> MissingOnly { get; private set; }

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
                "Add to the pinned recipes, once two or more are pinned, a list totalling the resources they require.");

            SortButton = config.Bind(
                "Inventory", "SortButton", true,
                "Add a Sort button to the inventory and to open containers.");

            SortBaseSlotsOnly = config.Bind(
                "Inventory", "SortBaseSlotsOnly", true,
                "Sort only the four vanilla rows of the player inventory, leaving the rows other mods add untouched.");

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

            CategoryFilters = config.Bind(
                "Inventory", "CategoryFilters", true,
                "Add category buttons above the inventory that grey out the items of every other category, in the inventory and open containers.");

            RecipeSearch = config.Bind(
                "Recipes", "RecipeSearch", true,
                "Narrow the recipe list of the crafting panel to what is typed in the inventory search field.");

            SmelterFill = config.Bind(
                "Stations", "SmelterFill", true,
                "Shift+E on the ore or fuel input of a smelting station loads it to its capacity in one press.");

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

            StoreAll = config.Bind(
                "Inventory", "StoreAll", true,
                "On a key press, empty the inventory into the chests in range, except equipped items, the hotbar and locked slots.");

            StoreAllKey = config.Bind(
                "Inventory", "StoreAllKey", new KeyboardShortcut(KeyCode.G, KeyCode.LeftShift),
                "Stores the carried items into the nearby chests, whether they already hold them or not.");

            StoreAllRange = config.Bind(
                "Inventory", "StoreAllRange", 10f,
                new ConfigDescription(
                    "Distance, in metres, within which chests take part in a store all.",
                    new AcceptableValueRange<float>(1f, 30f)));

            Restock = config.Bind(
                "Inventory", "Restock", true,
                "On a key press, top up the stacks of the hotbar and of the locked slots from the nearby chests. Needs CraftFromContainers.");

            RestockKey = config.Bind(
                "Inventory", "RestockKey", new KeyboardShortcut(KeyCode.T, KeyCode.LeftShift),
                "Tops up the stacks of the hotbar and of the locked slots from the nearby chests.");

            Favorites = config.Bind(
                "Inventory", "Favorites", true,
                "Lock inventory slots with a key: the sort, store all and the trash key leave them alone.");

            FavoriteKey = config.Bind(
                "Inventory", "FavoriteKey", new KeyboardShortcut(KeyCode.K),
                "Locks or unlocks the inventory slot under the pointer.");

            FavoriteSlots = config.Bind(
                "Inventory", "FavoriteSlots", string.Empty,
                "Locked slots, as column:row pairs separated by commas. Lock in game rather than here.");

            Trash = config.Bind(
                "Inventory", "Trash", true,
                "Destroy the stack under the pointer with a key, instead of dropping it.");

            TrashKey = config.Bind(
                "Inventory", "TrashKey", new KeyboardShortcut(KeyCode.Delete),
                "Destroys the stack of the player inventory under the pointer.");

            TrashConfirm = config.Bind(
                "Inventory", "TrashConfirm", true,
                "Ask for a second press of the trash key before destroying a stack.");

            CraftFromContainers = config.Bind(
                "Crafting", "CraftFromContainers", true,
                "Craft and build with the resources of the chests, carts and ships nearby: what the inventory lacks is fetched from them.");

            CraftFromContainersRange = config.Bind(
                "Crafting", "CraftFromContainersRange", 15f,
                new ConfigDescription(
                    "Distance, in metres, from the player or from a crafting station in range, within which chests lend their resources.",
                    new AcceptableValueRange<float>(1f, 50f)));

            PinRecipeKey = config.Bind(
                "Recipes", "PinRecipeKey", new KeyboardShortcut(KeyCode.P),
                "Pins or unpins the recipe of the item under the pointer, or else the one selected in the crafting panel.");

            PinMoreKey = config.Bind(
                "Recipes", "PinMoreKey", new KeyboardShortcut(KeyCode.PageUp),
                "Aims for one more craft of that recipe, pinning it first if needed.");

            PinLessKey = config.Bind(
                "Recipes", "PinLessKey", new KeyboardShortcut(KeyCode.PageDown),
                "Aims for one craft less of that recipe, never below one.");

            MissingOnly = config.Bind(
                "Recipes", "MissingOnly", false,
                "Show, under each pinned recipe, only the resources still missing.");

            PinnedRecipe = config.Bind(
                "Recipes", "PinnedRecipe", string.Empty,
                "Pinned recipes, by item prefab name, separated by commas, with ':n' after a name to aim for n crafts. Pin in game rather than here.");

            MaxPinnedRecipes = config.Bind(
                "Recipes", "MaxPinnedRecipes", 3,
                new ConfigDescription(
                    "How many recipes can be pinned at once.",
                    new AcceptableValueList<int>(1, 3, 5)));
        }
    }
}
