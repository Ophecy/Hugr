// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Hugr.Features.Sorting
{
    /// <summary>
    /// Merges what can be merged, then lays the items out by kind. Nothing is created, destroyed
    /// or converted: stacks are poured into each other and items change slot, which is what a
    /// player does by hand.
    /// Execution: client. Persistence: the inventory itself. Server interaction: none — a chest
    /// saves through the same <c>Inventory.Changed</c> path a drag and drop goes through, and no
    /// data of Hugr's own ever reaches the world.
    /// </summary>
    internal static class InventorySorter
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>();

        private static MethodInfo _changed;

        /// <summary>Checked once at startup, before any patch is installed.</summary>
        internal static void Verify()
        {
            _changed = AccessTools.Method(typeof(Inventory), "Changed")
                ?? throw new HugrException(
                    "HUGR-SORT-001", "Inventory no longer exposes the change notification.");
        }

        /// <summary>
        /// Sorts an inventory and returns how many slots that freed. The first
        /// <paramref name="keptRows"/> rows stay where they are — that is the hotbar, and a sort
        /// button that shuffles it is a sort button nobody presses twice. Their stacks are still
        /// topped up from the rest. Rows from <paramref name="rows"/> down are left alone
        /// altogether: neither merged nor moved.
        /// </summary>
        internal static int Sort(Inventory inventory, int keptRows, int rows)
        {
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            int freed = MergeStacks(items, rows);
            Arrange(inventory, items, keptRows, rows);
            _changed.Invoke(inventory, new object[] { false, false });
            return freed;
        }

        /// <summary>
        /// Pours every partial stack into the ones before it, walking the grid from the top left,
        /// so the hotbar fills first.
        /// </summary>
        private static int MergeStacks(List<ItemDrop.ItemData> items, int rows)
        {
            List<ItemDrop.ItemData> ordered = items.FindAll(item => item.m_gridPos.y < rows);
            ordered.Sort(ByGridPosition);

            int freed = 0;

            for (int target = 0; target < ordered.Count; target++)
            {
                ItemDrop.ItemData into = ordered[target];
                if (!Stackable(into))
                {
                    continue;
                }

                for (int source = ordered.Count - 1; source > target; source--)
                {
                    int room = into.m_shared.m_maxStackSize - into.m_stack;
                    if (room <= 0)
                    {
                        break;
                    }

                    ItemDrop.ItemData from = ordered[source];
                    if (!Stackable(from) || !SameKind(into, from))
                    {
                        continue;
                    }

                    int moved = Math.Min(room, from.m_stack);
                    into.m_stack += moved;
                    from.m_stack -= moved;

                    if (from.m_stack <= 0)
                    {
                        items.Remove(from);
                        ordered.RemoveAt(source);
                        freed++;
                    }
                }
            }

            return freed;
        }

        private static void Arrange(Inventory inventory, List<ItemDrop.ItemData> items, int keptRows, int rows)
        {
            List<ItemDrop.ItemData> movable = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in items)
            {
                if (item.m_gridPos.y >= keptRows && item.m_gridPos.y < rows)
                {
                    movable.Add(item);
                }
            }

            movable.Sort(ByKind);

            int width = inventory.GetWidth();
            int height = Math.Min(rows, inventory.GetHeight());
            int next = 0;

            for (int y = keptRows; y < height && next < movable.Count; y++)
            {
                for (int x = 0; x < width && next < movable.Count; x++)
                {
                    movable[next++].m_gridPos = new Vector2i(x, y);
                }
            }
        }

        private static int ByGridPosition(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return a.m_gridPos.y != b.m_gridPos.y
                ? a.m_gridPos.y - b.m_gridPos.y
                : a.m_gridPos.x - b.m_gridPos.x;
        }

        /// <summary>Groups by kind of item, then alphabetically, best and biggest first.</summary>
        private static int ByKind(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            int type = a.m_shared.m_itemType.CompareTo(b.m_shared.m_itemType);
            if (type != 0)
            {
                return type;
            }

            int name = string.Compare(Name(a), Name(b), StringComparison.CurrentCultureIgnoreCase);
            if (name != 0)
            {
                return name;
            }

            int quality = b.m_quality.CompareTo(a.m_quality);
            return quality != 0 ? quality : b.m_stack.CompareTo(a.m_stack);
        }

        private static string Name(ItemDrop.ItemData item)
        {
            string key = item.m_shared.m_name;
            if (!Names.TryGetValue(key, out string localized))
            {
                localized = Localization.instance == null ? key : Localization.instance.Localize(key);
                Names[key] = localized;
            }

            return localized;
        }

        private static bool Stackable(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared.m_maxStackSize > 1 && !item.m_equipped;
        }

        private static bool SameKind(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return a.m_shared.m_name == b.m_shared.m_name
                && a.m_quality == b.m_quality
                && a.m_variant == b.m_variant
                && a.m_worldLevel == b.m_worldLevel;
        }
    }
}
