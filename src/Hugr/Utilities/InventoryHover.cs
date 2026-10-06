// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Hugr.Utilities
{
    /// <summary>What the pointer is over in the open inventory panel.</summary>
    internal static class InventoryHover
    {
        /// <summary>
        /// The slot under the pointer, in the player's grid or in the open container's. A closed
        /// container keeps its grid where it was, so that grid only counts while one is open.
        /// </summary>
        internal static bool TryGetSlot(InventoryGui gui, out Inventory inventory, out Vector2i slot, out bool ofPlayer)
        {
            inventory = null;
            slot = default(Vector2i);
            ofPlayer = false;
            if (!InventoryGui.IsVisible())
            {
                return false;
            }

            InventoryElement element = gui.m_playerGrid.GetHoveredElement();
            ofPlayer = element != null;
            if (element == null && gui.IsContainerOpen())
            {
                element = gui.m_containerGrid.GetHoveredElement();
            }

            if (element == null)
            {
                return false;
            }

            inventory = (ofPlayer ? gui.m_playerGrid : gui.m_containerGrid).GetInventory();
            slot = element.Position;
            return inventory != null;
        }

        /// <summary>The item under the pointer, null over an empty slot or outside the grids.</summary>
        internal static ItemDrop.ItemData Item(InventoryGui gui, out bool ofPlayer)
        {
            return TryGetSlot(gui, out Inventory inventory, out Vector2i slot, out ofPlayer)
                ? inventory.GetItemAt(slot.x, slot.y)
                : null;
        }
    }
}
