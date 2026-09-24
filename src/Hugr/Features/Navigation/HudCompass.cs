// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hugr.Features.Navigation
{
    /// <summary>
    /// A compass strip across the top of the screen: the eight directions slide under a fixed
    /// centre mark as the camera turns. North is +Z, the minimap's own convention. The letters
    /// follow the game language, and the text is a clone of the minimap's biome label.
    /// Execution: client. Persistence: none. Server interaction: none.
    /// </summary>
    internal class HudCompass : MonoBehaviour
    {
        private const float Width = 480f;
        private const float Height = 36f;
        private const float TopMargin = 12f;

        /// <summary>Degrees of heading the strip spans, edge to edge.</summary>
        private const float FieldOfView = 180f;

        private static readonly Color North = new Color(1f, 0.63f, 0.24f);

        /// <summary>
        /// N, NE, E, SE, S, SW, W, NW per game language. Valheim has no strings for the cardinal
        /// points; a language missing here falls back to English.
        /// </summary>
        private static readonly Dictionary<string, string[]> Directions = new Dictionary<string, string[]>
        {
            { "English", new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" } },
            { "French", new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" } },
            { "Spanish", new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" } },
            { "Italian", new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" } },
            { "Portuguese_European", new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" } },
            { "Portuguese_Brazilian", new[] { "N", "NE", "L", "SE", "S", "SO", "O", "NO" } },
            { "German", new[] { "N", "NO", "O", "SO", "S", "SW", "W", "NW" } },
            { "Dutch", new[] { "N", "NO", "O", "ZO", "Z", "ZW", "W", "NW" } },
            { "Swedish", new[] { "N", "NO", "O", "SO", "S", "SV", "V", "NV" } },
            { "Norwegian", new[] { "N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV" } },
            { "Danish", new[] { "N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV" } },
            { "Russian", new[] { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" } },
        };

        private readonly List<Mark> _marks = new List<Mark>();

        private RectTransform _strip;
        private string _language;

        internal static void Bind(Harmony harmony)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.Compass,
                AccessTools.Method(typeof(Hud), nameof(Hud.Update)),
                AccessTools.Method(typeof(HudCompass), nameof(OnHudUpdate)));
        }

        /// <summary>Takes the compass off the HUD when the plugin unloads.</summary>
        internal static void Remove()
        {
            if (Hud.instance != null)
            {
                foreach (HudCompass compass in Hud.instance.GetComponents<HudCompass>())
                {
                    DestroyImmediate(compass);
                }
            }
        }

        private static void OnHudUpdate(Hud __instance)
        {
            if (__instance.GetComponent<HudCompass>() == null)
            {
                __instance.gameObject.AddComponent<HudCompass>();
            }
        }

        private void Update()
        {
            if (!ModConfig.Compass.Value)
            {
                Destroy(this);
                return;
            }

            try
            {
                Refresh();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(
                    exception is HugrException
                        ? exception.Message
                        : "HUGR-COMPASS-000: the compass was stopped (" + exception + ").");
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Refresh()
        {
            GameCamera camera = GameCamera.instance;
            bool shown = camera != null && Player.m_localPlayer != null && !Minimap.IsOpen();

            if (_strip != null)
            {
                _strip.gameObject.SetActive(shown);
            }

            if (!shown)
            {
                return;
            }

            string language = Localization.instance.GetSelectedLanguage();
            if (_strip == null || language != _language)
            {
                Build(language);
            }

            if (_strip == null)
            {
                return;
            }

            float heading = camera.transform.eulerAngles.y;
            float half = FieldOfView / 2f;
            foreach (Mark mark in _marks)
            {
                float delta = Mathf.DeltaAngle(heading, mark.Bearing);
                bool visible = Mathf.Abs(delta) <= half;
                mark.Text.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                mark.Text.rectTransform.anchoredPosition = new Vector2(delta / FieldOfView * Width, 0f);
                Color color = mark.Text.color;
                color.a = 1f - Mathf.Abs(delta) / half;
                mark.Text.color = color;
            }
        }

        private void Build(string language)
        {
            Clear();

            TMP_Text template = Minimap.instance == null ? null : Minimap.instance.m_biomeNameSmall;
            if (template == null || Hud.instance == null)
            {
                return;
            }

            _strip = new GameObject("HugrCompass", typeof(RectTransform), typeof(RectMask2D))
                .GetComponent<RectTransform>();
            _strip.SetParent(Hud.instance.m_rootObject.transform, false);
            _strip.anchorMin = new Vector2(0.5f, 1f);
            _strip.anchorMax = new Vector2(0.5f, 1f);
            _strip.pivot = new Vector2(0.5f, 1f);
            _strip.sizeDelta = new Vector2(Width, Height);
            _strip.anchoredPosition = new Vector2(0f, -TopMargin);

            string[] names = Directions.TryGetValue(language, out string[] local) ? local : Directions["English"];
            for (int i = 0; i < names.Length; i++)
            {
                bool cardinal = i % 2 == 0;
                AddMark(template, names[i], i * 45f, cardinal ? 1f : 0.75f, i == 0 ? North : Color.white);
                AddMark(template, "·", i * 45f + 22.5f, 0.75f, Color.white);
            }

            AddCentre(template);
            _language = language;
        }

        private void AddMark(TMP_Text template, string label, float bearing, float scale, Color color)
        {
            TMP_Text text = Widgets.CloneText(template, _strip, "HugrCompass_" + label);
            Centre(text.rectTransform);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = template.fontSize * scale;
            text.color = color;
            _marks.Add(new Mark(text, bearing));
        }

        /// <summary>The fixed mark the directions slide under.</summary>
        private void AddCentre(TMP_Text template)
        {
            TMP_Text centre = Widgets.CloneText(template, _strip, "HugrCompass_Centre");
            Centre(centre.rectTransform);
            centre.rectTransform.anchoredPosition = new Vector2(0f, -Height / 2f);
            centre.text = "|";
            centre.alignment = TextAlignmentOptions.Center;
            centre.fontSize = template.fontSize * 0.6f;
            centre.color = North;
        }

        private static void Centre(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(60f, Height);
            rect.anchoredPosition = Vector2.zero;
        }

        private void Clear()
        {
            _marks.Clear();
            _language = null;

            if (_strip != null)
            {
                Destroy(_strip.gameObject);
                _strip = null;
            }
        }

        private readonly struct Mark
        {
            internal Mark(TMP_Text text, float bearing)
            {
                Text = text;
                Bearing = bearing;
            }

            internal TMP_Text Text { get; }

            internal float Bearing { get; }
        }
    }
}
