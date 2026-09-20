// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Hugr.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace Hugr.UI
{
    /// <summary>
    /// Grafts a Hugr tab into Valheim's settings panel by cloning the vanilla tab button and a
    /// vanilla settings page, so the tab inherits the game's fonts, sprites and layout instead of
    /// imitating them. Nothing is looked up by hierarchy path: every template is discovered at
    /// runtime from the panel itself, which is what keeps this working across game updates.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: none.
    /// </summary>
    /// <remarks>
    /// The graft runs just before <c>Settings.SetAvailableTabs</c>, which rebuilds the panel's
    /// <c>SettingsTabs</c> list from <c>TabHandler.m_tabs</c> and picks each page's
    /// <see cref="ISettingsTab"/> component up on its own. Adding the tab there means the game
    /// registers, initializes and indexes the Hugr page exactly like its own, and
    /// <c>TabHandler.Init</c> — which runs later, on Start — wires the tab button for us.
    /// </remarks>
    [HarmonyPatch(typeof(Settings), "SetAvailableTabs")]
    internal static class SettingsTabInjector
    {
        private const string TabLabel = "Hugr";
        private const string PageName = "HugrSettings";

        private static void Prefix(Settings __instance)
        {
            try
            {
                Inject(__instance);
                Plugin.Log.LogInfo("Settings tab injected.");
            }
            catch (HugrException exception)
            {
                // The settings panel stays usable without the Hugr tab, so the panel is never
                // brought down with us: the failure is reported with its code instead.
                Plugin.Log.LogError(exception.Message);
            }
        }

        private static void Inject(Settings settings)
        {
            TabHandler tabHandler = Field<TabHandler>(settings, "m_tabHandler")
                ?? throw new HugrException("HUGR-UI-001", "Settings has no TabHandler.");

            List<TabHandler.Tab> tabs = tabHandler.m_tabs;
            if (tabs == null || tabs.Count == 0)
            {
                throw new HugrException("HUGR-UI-002", "Settings panel exposes no tab to clone.");
            }

            if (tabs.Exists(existing => existing.m_page != null && existing.m_page.name == PageName))
            {
                return;
            }

            RowTemplate template = FindRowTemplate(tabs);
            Transform row = BuildPage(template, out RectTransform page);
            HugrSettingsTab tab = page.gameObject.AddComponent<HugrSettingsTab>();

            AddRow(template, row, 0, tab, ModConfig.AutoRepair, "Automatic repair");
            AddRow(template, row, 1, tab, ModConfig.RepairAll, "Repair everything at once");
            AddRow(template, row, 2, tab, ModConfig.RecipeTracker, "Pinned recipes");
            AddRow(template, row, 3, tab, ModConfig.ShoppingList, "Shopping list");

            tabs.Add(new TabHandler.Tab
            {
                m_button = BuildTabButton(settings, tabs[0]),
                m_page = page,
                m_default = false,
                m_onClick = new UnityEvent(),
            });
        }

        /// <summary>
        /// Clones the page the row template came from and prunes it down to the branch leading to
        /// that single row. What survives is the vanilla page with its vanilla row container, so
        /// the rows keep the spacing, the anchors and the alignment of a real settings page.
        /// </summary>
        private static Transform BuildPage(RowTemplate template, out RectTransform page)
        {
            List<int> path = IndexPath(template.Row, template.Page);

            page = UnityEngine.Object.Instantiate(template.Page, template.Page.parent);
            page.gameObject.name = PageName;
            page.gameObject.SetActive(false);

            foreach (ISettingsTab inherited in page.GetComponentsInChildren<ISettingsTab>(true))
            {
                UnityEngine.Object.DestroyImmediate(inherited as Component);
            }

            Transform row = Resolve(page, path)
                ?? throw new HugrException("HUGR-UI-011", "The cloned page lost its row template.");

            for (Transform node = row; node != page; node = node.parent)
            {
                Transform parent = node.parent;
                for (int index = parent.childCount - 1; index >= 0; index--)
                {
                    Transform child = parent.GetChild(index);
                    if (child != node)
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }
            }

            return row;
        }

        private static Button BuildTabButton(Settings settings, TabHandler.Tab template)
        {
            if (template.m_button == null)
            {
                throw new HugrException("HUGR-UI-005", "Template tab has no button to clone.");
            }

            Button button = UnityEngine.Object.Instantiate(template.m_button, template.m_button.transform.parent);
            button.gameObject.name = "HugrTab";

            // The clone inherits the template's wiring; TabHandler.Init re-binds it on Start.
            button.onClick = new Button.ButtonClickedEvent();
            StripGamepadHints(settings, button.gameObject);
            SetTabLabel(button.gameObject, TabLabel);

            return button;
        }

        /// <summary>
        /// Drops the gamepad key hint the tab button carries. Only the hints registered in
        /// <c>Settings.m_tabKeyHints</c> are hidden when no gamepad is connected, and a clone is
        /// not in that list: it would stay on screen for keyboard players.
        /// </summary>
        private static void StripGamepadHints(Settings settings, GameObject button)
        {
            GameObject[] hints = Field<GameObject[]>(settings, "m_tabKeyHints");
            if (hints == null)
            {
                return;
            }

            HashSet<string> names = new HashSet<string>();
            foreach (GameObject hint in hints)
            {
                if (hint != null)
                {
                    names.Add(hint.name);
                }
            }

            foreach (Transform child in button.GetComponentsInChildren<Transform>(true))
            {
                if (child != button.transform && names.Contains(child.name))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void AddRow(
            RowTemplate template, Transform row0, int index, HugrSettingsTab tab,
            ConfigEntry<bool> entry, string label)
        {
            GameObject row = index == 0
                ? row0.gameObject
                : UnityEngine.Object.Instantiate(row0.gameObject, row0.parent);

            row.name = "HugrRow_" + entry.Definition.Key;
            row.SetActive(true);

            // Stacks the rows the way the vanilla page does. A layout group on the inherited
            // container, if there is one, overrides this on its own.
            RectTransform rect = row.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    rect.anchoredPosition.x, template.TopY - (template.Step * index));
            }

            Toggle toggle = row.GetComponentInChildren<Toggle>(true)
                ?? throw new HugrException("HUGR-UI-007", "Cloned row lost its toggle.");

            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.isOn = entry.Value;

            TMP_Text caption = row.GetComponentInChildren<TMP_Text>(true)
                ?? throw new HugrException("HUGR-UI-008", "No text component found on " + row.name + ".");
            Rename(caption, label);

            tab.Add(toggle, entry);
        }

        /// <summary>
        /// Picks the smallest existing widget that carries both a toggle and its caption, and
        /// measures how the page stacks those rows.
        /// </summary>
        private static RowTemplate FindRowTemplate(List<TabHandler.Tab> tabs)
        {
            foreach (TabHandler.Tab candidate in tabs)
            {
                if (candidate.m_page == null)
                {
                    continue;
                }

                Toggle toggle = candidate.m_page.GetComponentInChildren<Toggle>(true);
                if (toggle == null)
                {
                    continue;
                }

                // ponytail: nearest ancestor holding both toggle and caption. Switch to an
                // explicit row prefab if a future settings layout nests them further apart.
                Transform row = toggle.transform;
                while (row.parent != null && row != candidate.m_page
                       && row.GetComponentInChildren<TMP_Text>(true) == null)
                {
                    row = row.parent;
                }

                return new RowTemplate(candidate.m_page, row);
            }

            throw new HugrException("HUGR-UI-009", "No vanilla toggle found to use as a row template.");
        }

        /// <summary>
        /// Renames every caption of a tab button: Valheim keeps a second copy inside the button's
        /// "Selected" child, and that one is the only one visible once the tab is open.
        /// </summary>
        private static void SetTabLabel(GameObject button, string text)
        {
            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length == 0)
            {
                throw new HugrException("HUGR-UI-006", "No text component found on " + button.name + ".");
            }

            foreach (TMP_Text label in labels)
            {
                Rename(label, text);
            }
        }

        private static void Rename(TMP_Text label, string text)
        {
            // Valheim rewrites captions from its localization table on enable, which would
            // overwrite a name that has no translation key.
            foreach (MonoBehaviour behaviour in label.GetComponents<MonoBehaviour>())
            {
                if (behaviour.GetType().Name.IndexOf("Localize", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }

            label.text = text;
        }

        private static List<int> IndexPath(Transform node, Transform root)
        {
            List<int> path = new List<int>();
            for (Transform step = node; step != root && step != null; step = step.parent)
            {
                path.Insert(0, step.GetSiblingIndex());
            }

            return path;
        }

        private static Transform Resolve(Transform root, List<int> path)
        {
            Transform node = root;
            foreach (int index in path)
            {
                if (node == null || index >= node.childCount)
                {
                    return null;
                }

                node = node.GetChild(index);
            }

            return node == root ? null : node;
        }

        private static T Field<T>(Settings settings, string name) where T : class
        {
            try
            {
                return AccessTools.FieldRefAccess<Settings, T>(name)(settings);
            }
            catch (Exception exception)
            {
                throw new HugrException(
                    "HUGR-UI-010", "Settings." + name + " is not reachable (" + exception.Message + ").");
            }
        }

        /// <summary>A vanilla toggle row, and the vertical rhythm of the page it lives on.</summary>
        private readonly struct RowTemplate
        {
            internal RowTemplate(RectTransform page, Transform row)
            {
                Page = page;
                Row = row;

                RectTransform rect = row as RectTransform;
                TopY = rect != null ? rect.anchoredPosition.y : 0f;
                Step = MeasureStep(row, rect);
            }

            internal RectTransform Page { get; }

            internal Transform Row { get; }

            /// <summary>Where the first row of a vanilla page sits.</summary>
            internal float TopY { get; }

            /// <summary>Vertical distance between two rows of a vanilla page.</summary>
            internal float Step { get; }

            private static float MeasureStep(Transform row, RectTransform rect)
            {
                float fallback = rect != null ? rect.rect.height + 8f : 54f;
                if (row.parent == null || rect == null)
                {
                    return fallback;
                }

                float nearest = float.MaxValue;
                foreach (Transform sibling in row.parent)
                {
                    RectTransform other = sibling as RectTransform;
                    if (other == null || other == rect || sibling.GetComponentInChildren<Toggle>(true) == null)
                    {
                        continue;
                    }

                    float distance = Mathf.Abs(other.anchoredPosition.y - rect.anchoredPosition.y);
                    if (distance > 1f && distance < nearest)
                    {
                        nearest = distance;
                    }
                }

                return nearest < float.MaxValue ? nearest : fallback;
            }
        }
    }
}
