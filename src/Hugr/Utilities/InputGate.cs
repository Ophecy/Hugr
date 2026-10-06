// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Hugr.Utilities
{
    /// <summary>When a Hugr key may act: the gate vanilla puts on its own inventory key.</summary>
    internal static class InputGate
    {
        /// <summary>
        /// False while the player types — chat, console, text prompt, the build menu's search or
        /// the inventory search, which reports itself as the chat — or stands in a menu or the
        /// map. A postfix on <c>InventoryGui.Update</c> also runs when vanilla bails out early on
        /// a dead or teleporting player, hence the checks on the player.
        /// </summary>
        internal static bool TakesInput()
        {
            Player player = Player.m_localPlayer;
            return player != null
                && !player.IsDead()
                && !player.IsTeleporting()
                && (Chat.instance == null || !Chat.instance.HasFocus())
                && (Hud.instance == null || !Hud.instance.m_buildUi.SearchFieldFocused)
                && !Console.IsVisible()
                && !TextInput.IsVisible()
                && !Menu.IsVisible()
                && !Minimap.IsOpen();
        }
    }
}
