// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Hugr.Configuration;

namespace Hugr
{
    /// <summary>
    /// Entry point: binds the configuration and applies the patches.
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
            _harmony.PatchAll();
            Logger.LogInfo("Hugr loaded.");
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
