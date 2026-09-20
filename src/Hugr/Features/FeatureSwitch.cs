// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace Hugr.Features
{
    /// <summary>
    /// Installs a feature's Harmony patch only while its config entry is on, and follows the
    /// entry afterwards. A feature the player turned off has no patch on the game at all, so it
    /// cannot cost anything nor interfere with another mod.
    /// </summary>
    internal static class FeatureSwitch
    {
        internal static void Bind(Harmony harmony, ConfigEntry<bool> entry, MethodBase target, MethodInfo postfix)
        {
            if (target == null)
            {
                throw new HugrException(
                    "HUGR-PATCH-001", "Patch target of " + entry.Definition.Key + " no longer exists in the game.");
            }

            bool applied = false;

            void Sync()
            {
                if (entry.Value == applied)
                {
                    return;
                }

                if (entry.Value)
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    harmony.Unpatch(target, postfix);
                }

                applied = entry.Value;
                Plugin.Log.LogInfo(entry.Definition.Key + (applied ? " enabled." : " disabled."));
            }

            Sync();
            entry.SettingChanged += (sender, args) => Sync();
        }
    }
}
