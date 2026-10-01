// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using Hugr.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hugr.Features.Recipes
{
    /// <summary>
    /// Keeps the pinned recipe and its resources on screen while the player explores. The rows
    /// are clones of the crafting panel's own requirement widgets, so the icons, the font and the
    /// colours are the game's.
    /// Execution: client. Persistence: client (BepInEx config). Server interaction: none — the
    /// counts are read from the local inventory.
    /// </summary>
    internal class RecipeTrackerHud : MonoBehaviour
    {
        /// <summary>
        /// ponytail: polled instead of listening to Inventory.Changed, four times a second is
        /// imperceptible and survives every way an inventory can change. Hook the event if a
        /// profiler ever complains.
        /// </summary>
        private const float RefreshInterval = 0.25f;

        private static readonly Color Missing = new Color(1f, 0.5f, 0.4f);

        private readonly List<Row> _rows = new List<Row>();

        private RectTransform _panel;
        private TMP_Text _title;
        private Recipe _recipe;
        private float _timer;

        /// <summary>Attaches the tracker to the HUD, once per HUD.</summary>
        internal static void Ensure()
        {
            Hud hud = Hud.instance;
            if (hud != null && hud.GetComponent<RecipeTrackerHud>() == null)
            {
                hud.gameObject.AddComponent<RecipeTrackerHud>();
            }
        }

        /// <summary>
        /// Takes the tracker off the HUD. Called when the plugin unloads, so a hot reload does
        /// not leave the previous build's tracker running next to the new one.
        /// </summary>
        internal static void Remove()
        {
            Hud hud = Hud.instance;
            if (hud == null)
            {
                return;
            }

            foreach (RecipeTrackerHud tracker in hud.GetComponents<RecipeTrackerHud>())
            {
                DestroyImmediate(tracker);
            }
        }

        private void Update()
        {
            if (!ModConfig.RecipeTracker.Value)
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
                // Reporting this every frame would drown the log, so the tracker steps aside
                // after saying why.
                Plugin.Log.LogError(
                    exception is HugrException
                        ? exception.Message
                        : "HUGR-RECIPE-006: the tracker was stopped (" + exception + ").");
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Refresh()
        {
            Recipe recipe = PinnedRecipe.Resolve();
            Player player = Player.m_localPlayer;

            if (recipe == null || player == null)
            {
                if (_panel != null)
                {
                    _panel.gameObject.SetActive(false);
                }

                _recipe = null;
                return;
            }

            if (recipe != _recipe || _panel == null)
            {
                Build(recipe);
            }

            if (_panel == null)
            {
                return;
            }

            _panel.gameObject.SetActive(true);
            UpdateAmounts(recipe, player.GetInventory());
        }

        private void Build(Recipe recipe)
        {
            Clear();

            InventoryGui gui = InventoryGui.instance;
            GameObject[] requirements = gui == null ? null : gui.m_recipeRequirementList;
            if (requirements == null || requirements.Length == 0 || requirements[0] == null)
            {
                // The crafting panel has not been created yet: try again on the next tick.
                return;
            }

            GameObject template = requirements[0];
            RectTransform templateRect = template.transform as RectTransform;
            float rowHeight = templateRect != null && templateRect.rect.height > 1f
                ? templateRect.rect.height
                : 44f;

            Transform root = Hud.instance != null && Hud.instance.m_rootObject != null
                ? Hud.instance.m_rootObject.transform
                : transform;

            _panel = new GameObject("HugrRecipeTracker", typeof(RectTransform)).GetComponent<RectTransform>();
            _panel.SetParent(root, false);
            Anchor(_panel, new Vector2(-40f, -300f), new Vector2(280f, 0f));

            _title = Clone<TMP_Text>(template, "res_name", _panel, "HUGR-RECIPE-002");
            Anchor(_title.rectTransform, Vector2.zero, new Vector2(280f, rowHeight));
            _title.alignment = TextAlignmentOptions.Right;

            float offset = rowHeight + 4f;
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null || requirement.m_upgraderResource
                    || requirement.GetAmount(1) <= 0)
                {
                    continue;
                }

                GameObject row = Instantiate(template, _panel);
                row.name = "HugrResource_" + requirement.m_resItem.gameObject.name;
                row.SetActive(true);

                RectTransform rect = row.transform as RectTransform;
                if (rect != null)
                {
                    Anchor(rect, new Vector2(0f, -offset), new Vector2(280f, rowHeight));
                }

                Image icon = Find<Image>(row, "res_icon", "HUGR-RECIPE-003");
                icon.sprite = requirement.m_resItem.m_itemData.GetIcon();
                icon.enabled = true;

                TMP_Text name = Find<TMP_Text>(row, "res_name", "HUGR-RECIPE-002");
                name.text = Localization.instance.Localize(
                    requirement.m_resItem.m_itemData.m_shared.m_name);

                TMP_Text amount = Find<TMP_Text>(row, "res_amount", "HUGR-RECIPE-004");

                _rows.Add(new Row(requirement, name, amount));
                offset += rowHeight + 4f;
            }

            _recipe = recipe;
        }

        private void UpdateAmounts(Recipe recipe, Inventory inventory)
        {
            bool ready = true;

            foreach (Row row in _rows)
            {
                int have = inventory.CountItems(row.Requirement.m_resItem.m_itemData.m_shared.m_name, -1, true);
                int need = row.Requirement.GetAmount(1);
                bool enough = have >= need;

                row.Amount.text = have + " / " + need;
                row.Amount.color = enough ? Color.white : Missing;
                row.Name.color = enough ? Color.white : Missing;
                ready &= enough;
            }

            string name = Localization.instance.Localize(recipe.m_item.m_itemData.m_shared.m_name);
            if (recipe.m_amount > 1)
            {
                name += " x" + recipe.m_amount;
            }

            _title.text = ready ? name + " <color=#8CDF7F>v</color>" : name;
        }

        private void Clear()
        {
            _rows.Clear();
            _title = null;
            _recipe = null;

            if (_panel != null)
            {
                Destroy(_panel.gameObject);
                _panel = null;
            }
        }

        private static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static T Clone<T>(GameObject template, string child, Transform parent, string code)
            where T : Component
        {
            T source = Find<T>(template, child, code);
            T clone = Instantiate(source, parent);
            clone.gameObject.SetActive(true);
            return clone;
        }

        private static T Find<T>(GameObject row, string child, string code) where T : Component
        {
            Transform node = row.transform.Find(child);
            T component = node == null ? null : node.GetComponent<T>();
            if (component == null)
            {
                throw new HugrException(
                    code, "The crafting panel's requirement widget no longer carries " + child + ".");
            }

            return component;
        }

        private readonly struct Row
        {
            internal Row(Piece.Requirement requirement, TMP_Text name, TMP_Text amount)
            {
                Requirement = requirement;
                Name = name;
                Amount = amount;
            }

            internal Piece.Requirement Requirement { get; }

            internal TMP_Text Name { get; }

            internal TMP_Text Amount { get; }
        }
    }
}
