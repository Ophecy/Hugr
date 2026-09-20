// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;
using Hugr.UI;
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
                    "HUGR-SORT-002",
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
                Plugin.Log.LogError(
                    exception is HugrException
                        ? exception.Message
                        : "HUGR-SORT-000: unexpected failure while building the sort buttons ("
                          + exception + ").");
            }
        }

        private static void Build(InventoryGui gui)
        {
            if (_playerButton != null && _containerButton != null)
            {
                return;
            }

            Button template = gui.m_takeAllButton
                ?? throw new HugrException("HUGR-SORT-003", "The container panel has no button to clone.");

            if (_containerButton == null)
            {
                // Continues the row of container buttons, one step further along whatever
                // direction the vanilla ones are laid out in.
                _containerButton = Clone(template, template.transform.parent, ContainerButtonName);
                RectTransform rect = _containerButton.transform as RectTransform;
                RectTransform first = template.transform as RectTransform;
                RectTransform second = gui.m_stackAllButton == null
                    ? null
                    : gui.m_stackAllButton.transform as RectTransform;

                if (rect != null && first != null)
                {
                    Vector2 step = second == null
                        ? new Vector2(first.rect.width + 8f, 0f)
                        : second.anchoredPosition - first.anchoredPosition;
                    rect.anchoredPosition = (second == null ? first : second).anchoredPosition + step;
                }

                Wire(_containerButton, () => SortContainer(gui));
            }

            if (_playerButton == null && gui.m_player != null)
            {
                _playerButton = Clone(template, gui.m_player, PlayerButtonName);
                RectTransform rect = _playerButton.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    rect.anchoredPosition = new Vector2(-8f, 8f);
                }

                Wire(_playerButton, SortPlayer);
            }

            Plugin.Log.LogInfo("Sort buttons added.");
        }

        private static Button Clone(Button template, Transform parent, string name)
        {
            Button button = UnityEngine.Object.Instantiate(template, parent);
            button.gameObject.name = name;
            button.gameObject.SetActive(true);
            Widgets.StripScripts(button.gameObject);
            Widgets.SetLabel(button.gameObject, "Sort", "HUGR-SORT-004");
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
                    Plugin.Log.LogError("HUGR-SORT-005: the sort was refused (" + exception + ").");
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
            Report(player, InventorySorter.Sort(player.GetInventory(), 1));
        }

        private static void SortContainer(InventoryGui gui)
        {
            Container container = _currentContainer(gui);
            if (container == null)
            {
                return;
            }

            Report(Player.m_localPlayer, InventorySorter.Sort(container.GetInventory(), 0));
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
