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
    /// Grafts a Hugr tab into Valheim's settings panel by cloning a vanilla settings page and the
    /// vanilla tab button, so the tab inherits the game's fonts, sprites and layout instead of
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
    /// The graft is all-or-nothing: anything left half-built is destroyed again, because a
    /// stray clone in the panel is worse than no Hugr tab.
    /// </remarks>
    [HarmonyPatch(typeof(Settings), "SetAvailableTabs")]
    internal static class SettingsTabInjector
    {
        private const string TabLabel = "Hugr";
        private const string PageName = "HugrSettings";

        private static void Prefix(Settings __instance)
        {
            List<GameObject> clones = new List<GameObject>();

            try
            {
                Inject(__instance, clones);
            }
            catch (Exception exception)
            {
                foreach (GameObject clone in clones)
                {
                    if (clone != null)
                    {
                        UnityEngine.Object.DestroyImmediate(clone);
                    }
                }

                // This runs inside Settings.SetAvailableTabs: an exception escaping from here
                // would take the vanilla panel down with it. Nothing escapes, and what was half
                // built is gone, so the panel is exactly the one the game would have shown.
                Plugin.Log.LogError(
                    exception is HugrException
                        ? exception.Message
                        : "HUGR-UI-000: unexpected failure while injecting the tab (" + exception + ").");
            }
        }

        private static void Inject(Settings settings, List<GameObject> clones)
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

            Transform template = FindRowTemplate(tabs, out RectTransform sourcePage);
            Transform row = BuildPage(template, sourcePage, clones, out RectTransform page);
            HugrSettingsTab tab = page.gameObject.AddComponent<HugrSettingsTab>();

            SetUpRow(row.gameObject, tab, ModConfig.AutoRepair, "Automatic repair");
            AddRow(row, tab, ModConfig.RepairAll, "Repair everything at once");
            AddRow(row, tab, ModConfig.RecipeTracker, "Pinned recipes");
            AddRow(row, tab, ModConfig.ShoppingList, "Shopping list");
            AddRow(row, tab, ModConfig.SortButton, "Sort button");
            AddRow(row, tab, ModConfig.ServerPasswords, "Remember server passwords");
            AddRow(row, tab, ModConfig.Clock, "Clock");

            Button button = BuildTabButton(settings, tabs[0], clones);

            tabs.Add(new TabHandler.Tab
            {
                m_button = button,
                m_page = page,
                m_default = false,
                m_onClick = new UnityEvent(),
            });

            Plugin.Log.LogInfo(
                "Settings tab injected (page '" + sourcePage.name + "', row '" + template.name
                + "' under '" + template.parent.name + "', layout "
                + (row.parent.GetComponent<LayoutGroup>() == null ? "manual" : "inherited") + ").");
        }

        /// <summary>
        /// Clones the page the row template came from, strips the game scripts off the clone and
        /// hides everything that is not on the way to that one row. What survives is the vanilla
        /// page with its vanilla row container — including the layout group that positions the
        /// rows, which is the only thing that knows where a settings row belongs.
        /// </summary>
        /// <remarks>
        /// Siblings are hidden rather than destroyed: a kept Unity component, a scroll rect for
        /// instance, may still hold a reference to one of them.
        /// </remarks>
        private static Transform BuildPage(
            Transform template, RectTransform sourcePage, List<GameObject> clones, out RectTransform page)
        {
            List<int> path = IndexPath(template, sourcePage);
            if (path.Count == 0)
            {
                throw new HugrException(
                    "HUGR-UI-011", "The row template is the page itself, there is nothing to clone.");
            }

            page = UnityEngine.Object.Instantiate(sourcePage, sourcePage.parent);
            clones.Add(page.gameObject);
            page.gameObject.name = PageName;
            page.gameObject.SetActive(false);

            Widgets.StripScripts(page.gameObject);

            Transform row = Resolve(page, path)
                ?? throw new HugrException("HUGR-UI-011", "The cloned page lost its row template.");

            for (Transform node = row; node != page; node = node.parent)
            {
                foreach (Transform sibling in node.parent)
                {
                    if (sibling != node)
                    {
                        sibling.gameObject.SetActive(false);
                    }
                }
            }

            return row;
        }

        private static Button BuildTabButton(Settings settings, TabHandler.Tab template, List<GameObject> clones)
        {
            if (template.m_button == null)
            {
                throw new HugrException("HUGR-UI-005", "Template tab has no button to clone.");
            }

            Button button = UnityEngine.Object.Instantiate(template.m_button, template.m_button.transform.parent);
            clones.Add(button.gameObject);
            button.gameObject.name = "HugrTab";

            Widgets.StripScripts(button.gameObject);
            StripGamepadHint(settings, button.gameObject);

            // The clone inherits the template's wiring; TabHandler.Init re-binds it on Start.
            button.onClick = new Button.ButtonClickedEvent();
            Widgets.SetLabel(button.gameObject, TabLabel, "HUGR-UI-006");

            return button;
        }

        /// <summary>
        /// Drops the gamepad key hint the tab button carries. Only the hints registered in
        /// <c>Settings.m_tabKeyHints</c> are hidden when no gamepad is connected, and a clone is
        /// not in that list: it would stay on screen for keyboard players.
        /// </summary>
        private static void StripGamepadHint(Settings settings, GameObject button)
        {
            GameObject[] hints = TryField<GameObject[]>(settings, "m_tabKeyHints");
            if (hints == null)
            {
                Plugin.Log.LogWarning(
                    "HUGR-UI-012: Settings.m_tabKeyHints is unreachable, the Hugr tab may show a "
                    + "leftover gamepad hint.");
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
                if (child != null && child != button.transform && names.Contains(child.name))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>Adds a row next to the one kept from the vanilla page, which lays it out.</summary>
        private static void AddRow(Transform row0, HugrSettingsTab tab, ConfigEntry<bool> entry, string label)
        {
            GameObject row = UnityEngine.Object.Instantiate(row0.gameObject, row0.parent);
            SetUpRow(row, tab, entry, label);
        }

        private static void SetUpRow(GameObject row, HugrSettingsTab tab, ConfigEntry<bool> entry, string label)
        {
            row.name = "HugrRow_" + entry.Definition.Key;
            row.SetActive(true);

            Toggle toggle = row.GetComponentInChildren<Toggle>(true)
                ?? throw new HugrException(
                    "HUGR-UI-007", "The cloned row has no toggle, it carries " + Widgets.Describe(row) + ".");

            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.isOn = entry.Value;

            TMP_Text caption = row.GetComponentInChildren<TMP_Text>(true)
                ?? throw new HugrException(
                    "HUGR-UI-008", "The cloned row has no caption, it carries " + Widgets.Describe(row) + ".");
            caption.text = label;

            tab.Add(toggle, entry);
        }

        /// <summary>
        /// Picks the smallest existing widget that carries both a toggle and its caption: that is
        /// a settings row, whatever the game calls it.
        /// </summary>
        private static Transform FindRowTemplate(List<TabHandler.Tab> tabs, out RectTransform page)
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

                if (row == candidate.m_page)
                {
                    // Caption and toggle are not grouped on this page: there is no row to clone.
                    continue;
                }

                page = candidate.m_page;
                return row;
            }

            throw new HugrException("HUGR-UI-009", "No vanilla toggle found to use as a row template.");
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
            return TryField<T>(settings, name)
                ?? throw new HugrException("HUGR-UI-010", "Settings." + name + " is not reachable.");
        }

        private static T TryField<T>(Settings settings, string name) where T : class
        {
            try
            {
                return AccessTools.FieldRefAccess<Settings, T>(name)(settings);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
