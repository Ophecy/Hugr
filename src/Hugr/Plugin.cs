// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Features.Navigation;
using Hugr.Features.Recipes;
using Hugr.Features.Repair;
using Hugr.Features.Search;
using Hugr.Features.Servers;
using Hugr.Features.Sorting;
using Hugr.UI;

namespace Hugr
{
    /// <summary>
    /// Entry point: binds the configuration, grafts the settings tab and hands each feature its
    /// own switch. Features are independent, so one that fails to bind never takes another down.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ophecy.Hugr";
        public const string PluginName = "Hugr";
        public const string PluginVersion = BuildInfo.Version;

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        internal static ManualLogSource Log { get; private set; }

        /// <summary>
        /// Logs only while the debug mode is on, at info level: BepInEx filters the debug level out
        /// of its console and its log file by default.
        /// </summary>
        internal static void Trace(string message)
        {
            if (ModConfig.DebugMode.Value)
            {
                Log.LogInfo("[debug] " + message);
            }
        }

        private void Awake()
        {
            Log = Logger;
            ModConfig.Bind(Config);

            _harmony.PatchAll(typeof(SettingsTabInjector));
            Bind(AutoRepair.Bind);
            Bind(RepairAll.Bind);
            Bind(RecipePinning.Bind);
            Bind(SortButtons.Bind);
            Bind(ServerPasswords.Bind);
            Bind(HudClock.Bind);
            Bind(HudCompass.Bind);
            Bind(InventorySearch.Bind);

            Logger.LogInfo("Hugr " + BuildInfo.DisplayVersion + " loaded.");
        }

        private void Bind(Action<Harmony> feature)
        {
            try
            {
                feature(_harmony);
            }
            catch (Exception exception)
            {
                // One feature that cannot bind never takes the others, or the game, down.
                Logger.LogError(
                    exception is HugrException
                        ? exception.Message
                        : "HUGR-PATCH-000: a feature could not be bound (" + exception + ").");
            }
        }

        /// <summary>
        /// Leaves the game as it was found: patches removed, widgets removed. This is what makes
        /// a hot reload clean rather than a pile of two builds running side by side.
        /// </summary>
        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
            RecipeTrackerHud.Remove();
            SortButtons.Remove();
            HudClock.Remove();
            HudCompass.Remove();
            InventorySearch.Remove();
        }
    }
}
