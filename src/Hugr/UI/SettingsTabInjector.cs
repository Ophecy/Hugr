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
    /// The graft is all-or-nothing: anything left half-built is destroyed again, because a
    /// stray clone in the panel is worse than no Hugr tab. Nothing cloned keeps its game
    /// scripts either — a page that still carried a vanilla settings tab would be registered
    /// in its place and would lock the panel shut.
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

            RowTemplate template = FindRowTemplate(tabs);
            RectTransform page = BuildPage(template, clones, out RectTransform content);
            HugrSettingsTab tab = page.gameObject.AddComponent<HugrSettingsTab>();

            AddRow(template, content, 0, tab, ModConfig.AutoRepair, "Automatic repair");
            AddRow(template, content, 1, tab, ModConfig.RepairAll, "Repair everything at once");
            AddRow(template, content, 2, tab, ModConfig.RecipeTracker, "Pinned recipes");
            AddRow(template, content, 3, tab, ModConfig.ShoppingList, "Shopping list");

            Align(template, content);

            Button button = BuildTabButton(settings, tabs[0], clones);

            tabs.Add(new TabHandler.Tab
            {
                m_button = button,
                m_page = page,
                m_default = false,
                m_onClick = new UnityEvent(),
            });

            Plugin.Log.LogInfo(
                "Settings tab injected (page '" + template.Page.name + "', row '" + template.Row.name
                + "' at " + template.TopY + ", step " + template.Step + ", under "
                + template.ContainerPath + ").");
        }

        /// <summary>
        /// Builds the page as an empty rectangle that copies the vanilla page, then the same for
        /// every container the vanilla rows sit in. Copying the rectangles rather than the page
        /// itself reproduces the vanilla coordinates without inheriting a single game script, so
        /// nothing on the Hugr page can still believe it is driving the game's options.
        /// </summary>
        private static RectTransform BuildPage(
            RowTemplate template, List<GameObject> clones, out RectTransform content)
        {
            RectTransform page = Empty(PageName, template.Page.parent, template.Page);
            clones.Add(page.gameObject);
            page.gameObject.SetActive(false);

            // One empty rectangle per level of the vanilla chain: a row then lands at exactly
            // the coordinates it had in the vanilla page, whatever nesting that page uses.
            content = page;
            foreach (RectTransform level in template.Containers)
            {
                content = Empty(level.name, content, level);
            }

            return page;
        }

        /// <summary>
        /// Puts the first Hugr row exactly where the vanilla row it was cloned from sits. The
        /// rebuilt chain of rectangles is meant to do that on its own; measuring what is left and
        /// correcting it once means a page that nests or anchors its rows differently still lands
        /// right, instead of relying on the chain being a faithful copy.
        /// </summary>
        private static void Align(RowTemplate template, RectTransform content)
        {
            if (content.childCount == 0)
            {
                return;
            }

            Vector3 drift = template.Row.position - content.GetChild(0).position;
            if (drift.sqrMagnitude < 0.0001f)
            {
                return;
            }

            content.position += drift;
            Plugin.Log.LogInfo("Hugr rows realigned by " + drift + ".");
        }

        private static RectTransform Empty(string name, Transform parent, RectTransform model)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = model.anchorMin;
            rect.anchorMax = model.anchorMax;
            rect.pivot = model.pivot;
            rect.sizeDelta = model.sizeDelta;
            rect.anchoredPosition3D = model.anchoredPosition3D;
            rect.localScale = model.localScale;
            rect.localRotation = model.localRotation;
            return rect;
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

            StripScripts(button.gameObject);
            StripGamepadHint(settings, button.gameObject);

            // The clone inherits the template's wiring; TabHandler.Init re-binds it on Start.
            button.onClick = new Button.ButtonClickedEvent();
            SetTabLabel(button.gameObject, TabLabel);

            return button;
        }

        /// <summary>
        /// Removes every game script from a clone and keeps only the Unity UI machinery. A cloned
        /// widget otherwise drags along behaviours that still believe in the hierarchy they were
        /// built for: a settings page that writes into the game's options, a localizer that
        /// rewrites the caption, a gamepad handler that registers our button as one of the
        /// vanilla ones.
        /// </summary>
        private static void StripScripts(GameObject clone)
        {
            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && !IsWidget(behaviour.GetType()))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }
        }

        /// <summary>
        /// A widget is anything Unity's UI knows how to drive, including the game's own subclasses
        /// of it — Valheim's settings rows are built on <c>GUIFramework.GuiToggle</c>, a
        /// <see cref="Toggle"/>, and dropping it would leave a row that no longer toggles
        /// anything. Only behaviours that go straight to <see cref="MonoBehaviour"/> are game
        /// logic.
        /// </summary>
        private static bool IsWidget(Type type)
        {
            for (Type step = type; step != null && step != typeof(MonoBehaviour); step = step.BaseType)
            {
                string space = step.Namespace ?? string.Empty;
                if (space.StartsWith("UnityEngine", StringComparison.Ordinal)
                    || space.StartsWith("TMPro", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        private static void AddRow(
            RowTemplate template, RectTransform content, int index, HugrSettingsTab tab,
            ConfigEntry<bool> entry, string label)
        {
            GameObject row = UnityEngine.Object.Instantiate(template.Row.gameObject, content);
            row.name = "HugrRow_" + entry.Definition.Key;
            row.SetActive(true);
            StripScripts(row);

            // Stacks the rows the way the vanilla page does, from where its own first row sits.
            RectTransform rect = row.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    rect.anchoredPosition.x, template.TopY - (template.Step * index));
            }

            Toggle toggle = row.GetComponentInChildren<Toggle>(true)
                ?? throw new HugrException(
                    "HUGR-UI-007",
                    "The clone of row '" + template.Row.name + "' has no toggle, it carries "
                    + Describe(row) + ".");

            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.isOn = entry.Value;

            TMP_Text caption = row.GetComponentInChildren<TMP_Text>(true)
                ?? throw new HugrException(
                    "HUGR-UI-008",
                    "The clone of row '" + template.Row.name + "' has no caption, it carries "
                    + Describe(row) + ".");
            caption.text = label;

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

                if (row == candidate.m_page)
                {
                    // Caption and toggle are not grouped on this page: there is no row to clone.
                    continue;
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
                label.text = text;
            }
        }

        /// <summary>Names what a clone is made of, so a broken graft says so in one log line.</summary>
        private static string Describe(GameObject clone)
        {
            List<string> parts = new List<string>();
            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component != null && parts.Count < 24)
                {
                    parts.Add(component.GetType().Name);
                }
            }

            return string.Join(", ", parts.ToArray());
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

        /// <summary>A vanilla toggle row, and the vertical rhythm of the page it lives on.</summary>
        private readonly struct RowTemplate
        {
            internal RowTemplate(RectTransform page, Transform row)
            {
                Page = page;
                Row = row;

                Containers = new List<RectTransform>();
                for (Transform node = row.parent; node != null && node != page; node = node.parent)
                {
                    RectTransform level = node as RectTransform;
                    if (level != null)
                    {
                        Containers.Insert(0, level);
                    }
                }

                RectTransform rect = row as RectTransform;
                TopY = rect != null ? rect.anchoredPosition.y : 0f;
                Step = MeasureStep(row, rect);
            }

            internal RectTransform Page { get; }

            internal Transform Row { get; }

            /// <summary>Every container between the page and the row, outermost first.</summary>
            internal List<RectTransform> Containers { get; }

            internal string ContainerPath
            {
                get
                {
                    List<string> names = new List<string>();
                    foreach (RectTransform level in Containers)
                    {
                        names.Add(level.name);
                    }

                    return names.Count == 0 ? "the page" : string.Join("/", names.ToArray());
                }
            }

            /// <summary>Where the first row of a vanilla page sits.</summary>
            internal float TopY { get; }

            /// <summary>Vertical distance between two rows of a vanilla page.</summary>
            internal float Step { get; }

            private static float MeasureStep(Transform row, RectTransform rect)
            {
                float fallback = rect != null && rect.rect.height > 1f ? rect.rect.height + 8f : 54f;
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
