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
    /// Keeps the pinned recipes and their resources on screen while the player explores, one
    /// block per recipe, followed — with the shopping list on and two recipes or more — by a
    /// block totalling what they require together. The rows are clones of the crafting panel's
    /// own requirement widgets, so the icons, the font and the colours are the game's.
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

        private const float Width = 280f;

        private const float RowGap = 4f;

        private const float BlockGap = 12f;

        private static readonly Color Missing = new Color(1f, 0.5f, 0.4f);

        private readonly List<Block> _blocks = new List<Block>();

        private RectTransform _panel;

        /// <summary>The pins and the shopping list setting the panel was built from.</summary>
        private string _built;

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
                Plugin.Report(ErrorCodes.RecipeTrackerStopped, "the tracker was stopped", exception);
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Refresh()
        {
            List<Recipe> recipes = PinnedRecipe.ResolveAll();
            Player player = Player.m_localPlayer;

            if (recipes.Count == 0 || player == null)
            {
                if (_panel != null)
                {
                    _panel.gameObject.SetActive(false);
                }

                return;
            }

            // The count tells a pin the game did not know yet from one it now resolves.
            string wanted = ModConfig.PinnedRecipe.Value + "|" + recipes.Count + "|" + ModConfig.ShoppingList.Value;
            if (wanted != _built || _panel == null)
            {
                Build(recipes, wanted);
            }

            if (_panel == null)
            {
                return;
            }

            _panel.gameObject.SetActive(true);
            UpdateAmounts(player.GetInventory());
        }

        private void Build(List<Recipe> recipes, string wanted)
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
            Anchor(_panel, new Vector2(-40f, -300f), new Vector2(Width, 0f));

            List<Need> total = new List<Need>();
            float offset = 0f;
            foreach (Recipe recipe in recipes)
            {
                List<Need> needs = new List<Need>();
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    if (requirement == null || requirement.m_resItem == null || requirement.m_upgraderResource
                        || requirement.GetAmount(1) <= 0)
                    {
                        continue;
                    }

                    Add(needs, requirement.m_resItem, requirement.GetAmount(1));
                    Add(total, requirement.m_resItem, requirement.GetAmount(1));
                }

                string title = Localization.instance.Localize(recipe.m_item.m_itemData.m_shared.m_name);
                if (recipe.m_amount > 1)
                {
                    title += " x" + recipe.m_amount;
                }

                offset = AddBlock(template, rowHeight, offset, title, needs);
            }

            if (ModConfig.ShoppingList.Value && recipes.Count > 1)
            {
                AddBlock(template, rowHeight, offset, "Shopping list", total);
            }

            _built = wanted;
        }

        /// <summary>Adds the item to the list, or its amount to the line the list already has for it.</summary>
        private static void Add(List<Need> needs, ItemDrop item, int amount)
        {
            string name = item.m_itemData.m_shared.m_name;
            int index = needs.FindIndex(need => need.Item.m_itemData.m_shared.m_name == name);
            if (index < 0)
            {
                needs.Add(new Need(item, amount));
            }
            else
            {
                needs[index] = new Need(item, needs[index].Amount + amount);
            }
        }

        /// <summary>
        /// Lays a title and one row per need out from <paramref name="offset"/> down; returns where
        /// the next block starts.
        /// </summary>
        private float AddBlock(GameObject template, float rowHeight, float offset, string title, List<Need> needs)
        {
            TMP_Text caption = Clone<TMP_Text>(template, "res_name", _panel, ErrorCodes.RecipeTitleMissing);
            Anchor(caption.rectTransform, new Vector2(0f, -offset), new Vector2(Width, rowHeight));
            caption.alignment = TextAlignmentOptions.Right;
            offset += rowHeight + RowGap;

            List<Row> rows = new List<Row>();
            foreach (Need need in needs)
            {
                GameObject row = Instantiate(template, _panel);
                row.name = "HugrResource_" + need.Item.gameObject.name;
                row.SetActive(true);

                RectTransform rect = row.transform as RectTransform;
                if (rect != null)
                {
                    Anchor(rect, new Vector2(0f, -offset), new Vector2(Width, rowHeight));
                }

                Image icon = Find<Image>(row, "res_icon", ErrorCodes.RecipeIconMissing);
                icon.sprite = need.Item.m_itemData.GetIcon();
                icon.enabled = true;

                TMP_Text name = Find<TMP_Text>(row, "res_name", ErrorCodes.RecipeNameMissing);
                name.text = Localization.instance.Localize(need.Item.m_itemData.m_shared.m_name);

                TMP_Text amount = Find<TMP_Text>(row, "res_amount", ErrorCodes.RecipeAmountMissing);

                rows.Add(new Row(need, name, amount));
                offset += rowHeight + RowGap;
            }

            _blocks.Add(new Block(title, caption, rows));
            return offset + BlockGap;
        }

        private void UpdateAmounts(Inventory inventory)
        {
            foreach (Block block in _blocks)
            {
                bool ready = true;
                foreach (Row row in block.Rows)
                {
                    int have = inventory.CountItems(row.Need.Item.m_itemData.m_shared.m_name, -1, true);
                    bool enough = have >= row.Need.Amount;

                    row.Amount.text = have + " / " + row.Need.Amount;
                    row.Amount.color = enough ? Color.white : Missing;
                    row.Name.color = enough ? Color.white : Missing;
                    ready &= enough;
                }

                block.Caption.text = ready ? block.Title + " <color=#8CDF7F>v</color>" : block.Title;
            }
        }

        private void Clear()
        {
            _blocks.Clear();
            _built = null;

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

        /// <summary>An item and how many of it a recipe, or all of them together, take.</summary>
        private readonly struct Need
        {
            internal Need(ItemDrop item, int amount)
            {
                Item = item;
                Amount = amount;
            }

            internal ItemDrop Item { get; }

            internal int Amount { get; }
        }

        private readonly struct Block
        {
            internal Block(string title, TMP_Text caption, List<Row> rows)
            {
                Title = title;
                Caption = caption;
                Rows = rows;
            }

            internal string Title { get; }

            internal TMP_Text Caption { get; }

            internal List<Row> Rows { get; }
        }

        private readonly struct Row
        {
            internal Row(Need need, TMP_Text name, TMP_Text amount)
            {
                Need = need;
                Name = name;
                Amount = amount;
            }

            internal Need Need { get; }

            internal TMP_Text Name { get; }

            internal TMP_Text Amount { get; }
        }
    }
}
