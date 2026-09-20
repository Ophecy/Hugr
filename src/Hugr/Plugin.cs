// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Features.Repair;
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
        public const string PluginGuid = "com.ophecy.hugr";
        public const string PluginName = "Hugr";
        public const string PluginVersion = "0.1.0";

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            ModConfig.Bind(Config);

            _harmony.PatchAll(typeof(SettingsTabInjector));
            Bind(AutoRepair.Bind);

            Logger.LogInfo("Hugr loaded.");
        }

        private void Bind(Action<Harmony> feature)
        {
            try
            {
                feature(_harmony);
            }
            catch (HugrException exception)
            {
                Logger.LogError(exception.Message);
            }
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
