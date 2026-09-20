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
    /// Grafts a Hugr tab into Valheim's settings panel by cloning the vanilla tab button and
    /// page, so the tab inherits the game's fonts, sprites and layout instead of imitating them.
    /// Nothing is looked up by hierarchy path: every template is discovered at runtime from the
    /// panel itself, which is what keeps this working across game updates.
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

            GameObject rowTemplate = FindRowTemplate(tabs);
            TabHandler.Tab template = tabs[0];

            RectTransform page = BuildPage(template);
            HugrSettingsTab tab = page.gameObject.AddComponent<HugrSettingsTab>();

            AddRow(page, rowTemplate, tab, ModConfig.AutoRepair, "Automatic repair");
            AddRow(page, rowTemplate, tab, ModConfig.RepairAll, "Repair everything at once");
            AddRow(page, rowTemplate, tab, ModConfig.RecipeTracker, "Pinned recipes");
            AddRow(page, rowTemplate, tab, ModConfig.ShoppingList, "Shopping list");

            tabs.Add(new TabHandler.Tab
            {
                m_button = BuildTabButton(template),
                m_page = page,
                m_default = false,
                m_onClick = new UnityEvent(),
            });
        }

        /// <summary>
        /// Clones the first tab's page, strips it of the vanilla content and behaviour it was
        /// carrying, and leaves a vertically stacked container behind.
        /// </summary>
        private static RectTransform BuildPage(TabHandler.Tab template)
        {
            if (template.m_page == null)
            {
                throw new HugrException("HUGR-UI-004", "Template tab has no page to clone.");
            }

            RectTransform page = UnityEngine.Object.Instantiate(template.m_page, template.m_page.parent);
            page.gameObject.name = PageName;
            page.gameObject.SetActive(false);

            foreach (ISettingsTab inherited in page.GetComponentsInChildren<ISettingsTab>(true))
            {
                UnityEngine.Object.DestroyImmediate(inherited as Component);
            }

            foreach (Transform child in page)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            if (page.GetComponent<LayoutGroup>() == null)
            {
                VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlHeight = false;
                layout.childForceExpandHeight = false;
                layout.spacing = 8f;
                layout.padding = new RectOffset(24, 24, 24, 24);
            }

            return page;
        }

        private static Button BuildTabButton(TabHandler.Tab template)
        {
            if (template.m_button == null)
            {
                throw new HugrException("HUGR-UI-005", "Template tab has no button to clone.");
            }

            Button button = UnityEngine.Object.Instantiate(template.m_button, template.m_button.transform.parent);
            button.gameObject.name = "HugrTab";

            // The clone inherits the template's wiring; TabHandler.Init re-binds it on Start.
            button.onClick = new Button.ButtonClickedEvent();
            SetLabel(button.gameObject, TabLabel, "HUGR-UI-006");

            return button;
        }

        private static void AddRow(
            RectTransform page, GameObject rowTemplate, HugrSettingsTab tab,
            ConfigEntry<bool> entry, string label)
        {
            GameObject row = UnityEngine.Object.Instantiate(rowTemplate, page);
            row.gameObject.name = "HugrRow_" + entry.Definition.Key;
            row.SetActive(true);

            Toggle toggle = row.GetComponentInChildren<Toggle>(true)
                ?? throw new HugrException("HUGR-UI-007", "Cloned row lost its toggle.");

            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.isOn = entry.Value;

            SetLabel(row, label, "HUGR-UI-008");
            tab.Add(toggle, entry);
        }

        /// <summary>
        /// Picks the smallest existing widget that carries both a toggle and its caption, so the
        /// clone keeps the vanilla row layout rather than rebuilding one.
        /// </summary>
        private static GameObject FindRowTemplate(List<TabHandler.Tab> tabs)
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
                Transform node = toggle.transform;
                while (node != null && node != candidate.m_page)
                {
                    if (node.GetComponentInChildren<TMP_Text>(true) != null)
                    {
                        return node.gameObject;
                    }

                    node = node.parent;
                }

                return toggle.gameObject;
            }

            throw new HugrException("HUGR-UI-009", "No vanilla toggle found to use as a row template.");
        }

        private static void SetLabel(GameObject target, string text, string errorCode)
        {
            TMP_Text label = target.GetComponentInChildren<TMP_Text>(true)
                ?? throw new HugrException(errorCode, "No text component found on " + target.name + ".");

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
    }
}
