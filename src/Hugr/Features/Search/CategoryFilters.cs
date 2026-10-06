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

namespace Hugr.Features.Search
{
    /// <summary>
    /// A row of category buttons above the inventory, cloned from the container's own buttons.
    /// Pressing one greys out the items of every other category, in the inventory and in the open
    /// container; pressing it again shows everything. Items are greyed, never hidden, exactly as
    /// <see cref="InventorySearch"/> does, and both narrow the same grid when used together.
    /// Execution: client. Persistence: none — the filter is dropped when the inventory closes.
    /// Server interaction: none.
    /// </summary>
    internal static class CategoryFilters
    {
        private const float Gap = 6f;

        /// <summary>Alpha of the captions of the categories left out while one is selected.</summary>
        private const float Unselected = 0.4f;

        /// <summary>Smallest size a caption shrinks to; TextMeshPro's own floor is too high here.</summary>
        private const float MinFontSize = 8f;

        private static readonly List<Filter> Filters = new List<Filter>();

        private static Category? _selected;

        /// <summary>Width the row was last laid out on: the search field comes and goes beside it.</summary>
        private static float _laidOut;

        private static bool _stopped;

        private enum Category
        {
            Weapons,
            Armor,
            Tools,
            Food,
            Potions,
            Materials,
            Misc
        }

        internal static void Bind(Harmony harmony)
        {
            ModConfig.CategoryFilters.SettingChanged += (sender, args) =>
            {
                if (!ModConfig.CategoryFilters.Value)
                {
                    Remove();
                }
            };

            Patch(harmony, typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui), nameof(OnUpdateGui));
            Patch(harmony, typeof(InventoryGui), nameof(InventoryGui.Hide), nameof(OnHide));
        }

        /// <summary>Takes the buttons off the panel when the feature is switched off or unloaded.</summary>
        internal static void Remove()
        {
            foreach (Filter filter in Filters)
            {
                if (filter.Button != null)
                {
                    UnityEngine.Object.DestroyImmediate(filter.Button.gameObject);
                }
            }

            Filters.Clear();
            _selected = null;
        }

        private static void Patch(Harmony harmony, Type type, string target, string postfix)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.CategoryFilters,
                AccessTools.Method(type, target),
                AccessTools.Method(typeof(CategoryFilters), postfix));
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

                // The panel is destroyed with its scene and takes the buttons along.
                if (Filters.Count == 0 || Filters[0].Button == null)
                {
                    Remove();
                    Build(gui);
                }

                float width = gui.m_player.rect.width - InventorySearch.ReservedWidth;
                if (width != _laidOut)
                {
                    Place(width);
                }

                if (_selected.HasValue)
                {
                    Category selected = _selected.Value;
                    InventorySearch.Dim(__instance, item => Of(item.m_shared) == selected);
                }
            }
            catch (Exception exception)
            {
                // Runs every frame: the filters step aside for the session after saying why, the
                // inventory stays usable.
                Plugin.Report(ErrorCodes.FilterStopped, "the inventory category filters were stopped", exception);
                _stopped = true;
                Remove();
            }
        }

        private static void OnHide()
        {
            Select(null);
        }

        /// <summary>
        /// The category of an item, read off its vanilla type. A pickaxe is typed as a weapon and
        /// told apart by its skill; food is what vanilla itself marks with the fork icon.
        /// </summary>
        private static Category Of(ItemDrop.ItemData.SharedData shared)
        {
            switch (shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return shared.m_skillType == Skills.SkillType.Pickaxes ? Category.Tools : Category.Weapons;
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return Category.Armor;
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                    return Category.Tools;
                case ItemDrop.ItemData.ItemType.Consumable:
                    return shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f
                        ? Category.Food
                        : Category.Potions;
                case ItemDrop.ItemData.ItemType.Material:
                case ItemDrop.ItemData.ItemType.Fish:
                    return Category.Materials;
                default:
                    return Category.Misc;
            }
        }

        private static void Build(InventoryGui gui)
        {
            Button template = gui.m_takeAllButton;
            if (template == null || gui.m_player == null)
            {
                throw new HugrException(
                    ErrorCodes.FilterButtonTemplateMissing, "The container panel has no button to clone.");
            }

            foreach (Category category in Enum.GetValues(typeof(Category)))
            {
                Button button = UnityEngine.Object.Instantiate(template, gui.m_player);
                button.gameObject.name = "HugrFilter" + category;
                button.gameObject.SetActive(true);
                Widgets.StripGamepadHints(button.gameObject);
                Widgets.StripScripts(button.gameObject);
                Widgets.SetLabel(button.gameObject, category.ToString(), ErrorCodes.FilterLabelMissing);

                // Seven captions share the width of the panel: each one shrinks to fit its button.
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = MinFontSize;
                label.enableAutoSizing = true;

                Category pressed = category;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => Select(_selected == pressed ? (Category?)null : pressed));

                Filters.Add(new Filter(category, button, label));
            }

            _laidOut = 0f;
            Plugin.Log.LogInfo("Inventory category filters added.");
        }

        /// <summary>
        /// Just above the player panel's top edge, from its left edge, the buttons sharing
        /// <paramref name="width"/> evenly: the search field takes the right end of that row.
        /// </summary>
        private static void Place(float width)
        {
            float step = width / Filters.Count;
            for (int i = 0; i < Filters.Count; i++)
            {
                RectTransform rect = (RectTransform)Filters[i].Button.transform;
                float height = rect.rect.height;

                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(step, height);
                rect.anchoredPosition = new Vector2(i * step, Gap);
            }

            _laidOut = width;
        }

        /// <summary>Keeps one category, or none, and fades the captions of the others.</summary>
        private static void Select(Category? category)
        {
            _selected = category;
            foreach (Filter filter in Filters)
            {
                if (filter.Label == null)
                {
                    continue;
                }

                Color colour = filter.Colour;
                if (category.HasValue && filter.Category != category.Value)
                {
                    colour.a *= Unselected;
                }

                filter.Label.color = colour;
            }
        }

        private readonly struct Filter
        {
            internal Filter(Category category, Button button, TMP_Text label)
            {
                Category = category;
                Button = button;
                Label = label;
                Colour = label.color;
            }

            internal Category Category { get; }

            internal Button Button { get; }

            internal TMP_Text Label { get; }

            /// <summary>Colour of the caption as cloned, the one it returns to.</summary>
            internal Color Colour { get; }
        }
    }
}
