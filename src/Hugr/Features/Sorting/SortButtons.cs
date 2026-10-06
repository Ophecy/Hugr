// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.Features.Slots;
using Hugr.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hugr.Features.Sorting
{
    /// <summary>
    /// Puts a Sort button on the inventory and on any open container, cloned from the container's
    /// own buttons so it looks like one of them.
    /// Execution: client. Persistence: none. Server interaction: none.
    /// </summary>
    internal static class SortButtons
    {
        private const string PlayerButtonName = "HugrSortPlayer";
        private const string ContainerButtonName = "HugrSortContainer";

        /// <summary>
        /// Rows of the vanilla player inventory (<c>Humanoid</c> builds it 8 × 4); the rows a mod
        /// appends below them — equipment or quick slots — are its own business.
        /// </summary>
        private const int BaseRows = 4;

        private static AccessTools.FieldRef<InventoryGui, Container> _currentContainer;
        private static Button _playerButton;
        private static Button _containerButton;

        internal static void Bind(Harmony harmony)
        {
            InventorySorter.Verify();

            try
            {
                _currentContainer = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
            }
            catch (Exception exception)
            {
                throw new HugrException(
                    ErrorCodes.SortContainerUnreachable,
                    "InventoryGui.m_currentContainer is not reachable (" + exception.Message + ").");
            }

            ModConfig.SortButton.SettingChanged += (sender, args) =>
            {
                if (!ModConfig.SortButton.Value)
                {
                    Remove();
                }
            };

            FeatureSwitch.Bind(
                harmony,
                ModConfig.SortButton,
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.Show)),
                AccessTools.Method(typeof(SortButtons), nameof(OnShow)));
        }

        /// <summary>Takes the buttons off the panel when the feature is switched off or unloaded.</summary>
        internal static void Remove()
        {
            Destroy(ref _playerButton);
            Destroy(ref _containerButton);
        }

        private static void OnShow(InventoryGui __instance)
        {
            try
            {
                Build(__instance);
            }
            catch (Exception exception)
            {
                // The inventory must open whatever happens.
                Plugin.Report(
                    ErrorCodes.SortButtonsUnexpected, "unexpected failure while building the sort buttons", exception);
            }
        }

        private static void Build(InventoryGui gui)
        {
            if (_playerButton != null && _containerButton != null)
            {
                return;
            }

            Button template = gui.m_takeAllButton
                ?? throw new HugrException(
                    ErrorCodes.SortButtonTemplateMissing, "The container panel has no button to clone.");

            if (_containerButton == null)
            {
                _containerButton = Clone(template, template.transform.parent, ContainerButtonName);
                Place(_containerButton, gui.m_containerWeight);
                Wire(_containerButton, () => SortContainer(gui));
            }

            if (_playerButton == null && gui.m_player != null)
            {
                _playerButton = Clone(template, gui.m_player, PlayerButtonName);
                Place(_playerButton, gui.m_weight);
                Wire(_playerButton, SortPlayer);
            }

            Plugin.Log.LogInfo("Sort buttons added.");
        }

        /// <summary>
        /// Drops the button into the free column the panel's weight badge already occupies, just
        /// outside the right edge, one button height below the middle of that edge — clear of the weight badge
        /// pinned to the bottom and of the armor badge above it.
        /// </summary>
        /// <remarks>
        /// The badge is a sibling of the button, so its anchoring transfers as read, and it is
        /// the panel itself that says how far out and how wide that column is. Inside the panel
        /// there is no room: the grids fill them, and the container's two buttons already take
        /// its top row.
        /// </remarks>
        private static void Place(Button button, TMP_Text weight)
        {
            RectTransform badge = weight == null ? null : weight.transform.parent as RectTransform;
            if (badge == null)
            {
                throw new HugrException(
                    ErrorCodes.SortBadgeMissing, "The panel has no weight badge to line the sort button up with.");
            }

            RectTransform rect = (RectTransform)button.transform;
            float height = rect.rect.height;

            rect.anchorMin = new Vector2(badge.anchorMin.x, 0.5f);
            rect.anchorMax = new Vector2(badge.anchorMax.x, 0.5f);
            rect.pivot = new Vector2(badge.pivot.x, 0.5f);
            rect.sizeDelta = new Vector2(badge.rect.width, height);
            rect.anchoredPosition = new Vector2(badge.anchoredPosition.x, -height);
        }

        private static Button Clone(Button template, Transform parent, string name)
        {
            Button button = UnityEngine.Object.Instantiate(template, parent);
            button.gameObject.name = name;
            button.gameObject.SetActive(true);
            Widgets.StripGamepadHints(button.gameObject);
            Widgets.StripScripts(button.gameObject);
            Widgets.SetLabel(button.gameObject, "Sort", ErrorCodes.SortLabelMissing);
            return button;
        }

        private static void Wire(Button button, Action action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Plugin.Report(ErrorCodes.SortRefused, "the sort was refused", exception);
                }
            });
        }

        private static void SortPlayer()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            // Row zero is the hotbar: its stacks are topped up, its slots never move.
            Inventory inventory = player.GetInventory();
            int rows = ModConfig.SortBaseSlotsOnly.Value ? BaseRows : inventory.GetHeight();
            Report(player, InventorySorter.Sort(inventory, 1, rows, FavoriteSlots.IsFavorite));
        }

        private static void SortContainer(InventoryGui gui)
        {
            Container container = _currentContainer(gui);
            if (container == null)
            {
                return;
            }

            Inventory inventory = container.GetInventory();
            Report(Player.m_localPlayer, InventorySorter.Sort(inventory, 0, inventory.GetHeight(), slot => false));
        }

        private static void Report(Player player, int freed)
        {
            if (player == null)
            {
                return;
            }

            player.Message(
                MessageHud.MessageType.TopLeft,
                freed > 0 ? "Sorted, " + freed + " slot(s) freed" : "Sorted",
                0,
                null,
                false);
        }

        private static void Destroy(ref Button button)
        {
            if (button != null)
            {
                UnityEngine.Object.DestroyImmediate(button.gameObject);
            }

            button = null;
        }
    }
}
