// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.UI;
using TMPro;
using UnityEngine;

namespace Hugr.Features.Navigation
{
    /// <summary>
    /// Shows the day and the time of day just under the minimap, in a clone of the minimap's own
    /// biome label. The time is the world's: <c>EnvMan</c>'s day fraction, on the same scale as
    /// the sun, so 06:00 is sunrise and 18:00 sunset.
    /// Execution: client. Persistence: none. Server interaction: none — the world time is
    /// already synchronised to every client by vanilla.
    /// </summary>
    internal class HudClock : MonoBehaviour
    {
        /// <summary>A game minute lasts a bit more than a real second: once a second is enough.</summary>
        private const float RefreshInterval = 1f;

        private TMP_Text _text;
        private float _timer;

        internal static void Bind(Harmony harmony)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.Clock,
                AccessTools.Method(typeof(Hud), nameof(Hud.Update)),
                AccessTools.Method(typeof(HudClock), nameof(OnHudUpdate)));
        }

        /// <summary>Takes the clock off the HUD when the plugin unloads.</summary>
        internal static void Remove()
        {
            if (Hud.instance != null)
            {
                foreach (HudClock clock in Hud.instance.GetComponents<HudClock>())
                {
                    DestroyImmediate(clock);
                }
            }
        }

        private static void OnHudUpdate(Hud __instance)
        {
            if (__instance.GetComponent<HudClock>() == null)
            {
                __instance.gameObject.AddComponent<HudClock>();
            }
        }

        private void Update()
        {
            if (!ModConfig.Clock.Value)
            {
                Destroy(this);
                return;
            }

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
            {
                return;
            }

            _timer = RefreshInterval;

            try
            {
                Refresh();
            }
            catch (Exception exception)
            {
                Plugin.Report(ErrorCodes.ClockStopped, "the clock was stopped", exception);
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (_text != null)
            {
                Destroy(_text.gameObject);
            }
        }

        private void Refresh()
        {
            EnvMan env = EnvMan.instance;
            TMP_Text biome = Minimap.instance == null ? null : Minimap.instance.m_biomeNameSmall;
            if (env == null || biome == null || ZNet.instance == null)
            {
                return;
            }

            if (_text == null)
            {
                _text = Widgets.CloneText(biome, Hud.instance.m_rootObject.transform, "HugrClock");
            }

            Place(biome);

            float hours = env.GetDayFraction() * 24f;
            int hour = (int)hours;
            int minute = (int)((hours - hour) * 60f);
            _text.text = Localization.instance.Localize("$msg_newday", env.GetDay().ToString())
                         + "  " + hour.ToString("00") + ":" + minute.ToString("00");
        }

        /// <summary>
        /// One line under the biome label, followed every refresh so a resolution change carries
        /// the clock along. The clock hangs off the HUD root rather than the minimap: the minimap
        /// panel is switched off on no-map worlds, the clock must not go with it.
        /// </summary>
        private void Place(TMP_Text biome)
        {
            RectTransform source = biome.rectTransform;
            RectTransform rect = _text.rectTransform;
            rect.position = source.position - source.up * (source.rect.height * source.lossyScale.y);
        }
    }
}
