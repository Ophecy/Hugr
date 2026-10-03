// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.UI;
using TMPro;
using UnityEngine;

namespace Hugr.Features.Search
{
    /// <summary>
    /// A search field above the inventory, cloned from the build menu's own search field. Items
    /// of the inventory and of the open container that do not match are greyed out, never
    /// hidden: every item keeps its slot, so drag and drop stays what the player sees. Matching
    /// is the build menu's: localized name, lower case, spaces ignored.
    /// Execution: client. Persistence: none — the field is emptied when the inventory closes.
    /// Server interaction: none.
    /// </summary>
    internal static class InventorySearch
    {
        private const float Gap = 6f;

        private static readonly Color Dimmed = new Color(1f, 1f, 1f, 0.2f);

        private static TMP_InputField _field;

        private static bool _stopped;

        internal static void Bind(Harmony harmony)
        {
            ModConfig.InventorySearch.SettingChanged += (sender, args) =>
            {
                if (!ModConfig.InventorySearch.Value)
                {
                    Remove();
                }
            };

            Patch(harmony, typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui), nameof(OnUpdateGui));
            Patch(harmony, typeof(InventoryGui), nameof(InventoryGui.Hide), nameof(OnHide));
            Patch(harmony, typeof(Chat), nameof(Chat.HasFocus), nameof(OnHasFocus));
        }

        /// <summary>Takes the field off the panel when the feature is switched off or unloaded.</summary>
        internal static void Remove()
        {
            if (_field != null)
            {
                UnityEngine.Object.DestroyImmediate(_field.gameObject);
            }

            _field = null;
        }

        private static void Patch(Harmony harmony, Type type, string target, string postfix)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.InventorySearch,
                AccessTools.Method(type, target),
                AccessTools.Method(typeof(InventorySearch), postfix));
        }

        /// <summary>Runs every frame the grid is drawn, right after vanilla reset each icon's colour.</summary>
        private static void OnUpdateGui(InventoryGrid __instance)
        {
            try
            {
                InventoryGui gui = InventoryGui.instance;
                if (_stopped || gui == null || (__instance != gui.m_playerGrid && __instance != gui.m_containerGrid))
                {
                    return;
                }

                if (_field == null)
                {
                    Build(gui);
                }

                Dim(__instance, Normalize(_field.text));
            }
            catch (Exception exception)
            {
                // Runs every frame: the search steps aside for the session after saying why, the
                // inventory stays usable.
                Plugin.Report(ErrorCodes.SearchStopped, "the inventory search was stopped", exception);
                _stopped = true;
                Remove();
            }
        }

        private static void OnHide()
        {
            if (_field != null)
            {
                _field.text = string.Empty;
                _field.DeactivateInputField();
            }
        }

        /// <summary>
        /// Typing in the field counts as typing in the chat. Every vanilla system that stands
        /// back while the chat has focus — the inventory's own close keys (Tab, E), the camera,
        /// the player controls, the minimap — stands back for the search too.
        /// </summary>
        private static void OnHasFocus(ref bool __result)
        {
            if (!__result && _field != null && _field.isFocused)
            {
                __result = true;
            }
        }

        private static void Dim(InventoryGrid grid, string term)
        {
            if (term.Length == 0 || grid.m_inventory == null)
            {
                return;
            }

            int width = grid.m_inventory.GetWidth();
            foreach (ItemDrop.ItemData item in grid.m_inventory.GetAllItems())
            {
                string name = Normalize(Localization.instance.Localize(item.m_shared.m_name));
                if (!name.Contains(term))
                {
                    InventoryElement element = grid.GetElement(item.m_gridPos.x, item.m_gridPos.y, width);
                    if (element != null)
                    {
                        element.m_icon.color = Dimmed;
                    }
                }
            }
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static void Build(InventoryGui gui)
        {
            TMP_InputField template = Hud.instance == null ? null : Hud.instance.m_buildUi.m_searchField;
            if (template == null || gui.m_player == null)
            {
                throw new HugrException(
                    ErrorCodes.SearchTemplateMissing, "The build menu has no search field to clone.");
            }

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, gui.m_player, false);
            clone.name = "HugrInventorySearch";
            Widgets.StripGamepadHints(clone);
            Widgets.StripScripts(clone);
            clone.SetActive(true);

            _field = clone.GetComponent<TMP_InputField>()
                ?? throw new HugrException(
                    ErrorCodes.SearchFieldMissing, "The cloned search field carries " + Widgets.Describe(clone) + ".");

            // Listeners saved in the prefab still point at the build menu.
            _field.onValueChanged = new TMP_InputField.OnChangeEvent();
            _field.onEndEdit = new TMP_InputField.SubmitEvent();
            _field.onSubmit = new TMP_InputField.SubmitEvent();
            _field.onSelect = new TMP_InputField.SelectionEvent();
            _field.onDeselect = new TMP_InputField.SelectionEvent();
            _field.text = string.Empty;

            Place((RectTransform)clone.transform);
            Plugin.Log.LogInfo("Inventory search added.");
        }

        /// <summary>
        /// Just above the player panel's top edge, right-aligned, at the width the build menu gives
        /// its own field: inside the panel the grid takes every row.
        /// </summary>
        private static void Place(RectTransform rect)
        {
            float width = rect.rect.width;
            float height = rect.rect.height;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, Gap);
        }
    }
}
