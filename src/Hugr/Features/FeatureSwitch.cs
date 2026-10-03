// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
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
        /// <summary>
        /// Keeps <paramref name="patch"/> on <paramref name="target"/> while the entry is on: as a
        /// postfix, or as a prefix when <paramref name="prefix"/> is set.
        /// </summary>
        internal static void Bind(
            Harmony harmony, ConfigEntry<bool> entry, MethodBase target, MethodInfo patch, bool prefix = false)
        {
            BindAny(harmony, new[] { entry }, target, patch, prefix);
        }

        /// <summary>
        /// Keeps <paramref name="patch"/> on <paramref name="target"/> while any of the entries is
        /// on: for a patch that several features share, installed once whichever of them needs it.
        /// </summary>
        internal static void BindAny(
            Harmony harmony, ConfigEntry<bool>[] entries, MethodBase target, MethodInfo patch, bool prefix = false)
        {
            string keys = string.Join("/", entries.Select(entry => entry.Definition.Key));
            if (target == null)
            {
                throw new HugrException(
                    ErrorCodes.PatchTargetMissing, "Patch target of " + keys + " no longer exists in the game.");
            }

            bool applied = false;

            void Sync()
            {
                bool wanted = entries.Any(entry => entry.Value);
                if (wanted == applied)
                {
                    return;
                }

                if (wanted)
                {
                    HarmonyMethod method = new HarmonyMethod(patch);
                    harmony.Patch(target, prefix: prefix ? method : null, postfix: prefix ? null : method);
                }
                else
                {
                    harmony.Unpatch(target, patch);
                }

                applied = wanted;
                Plugin.Log.LogInfo(keys + (applied ? " enabled." : " disabled."));
            }

            Sync();
            foreach (ConfigEntry<bool> entry in entries)
            {
                entry.SettingChanged += (sender, args) => Sync();
            }
        }
    }
}
