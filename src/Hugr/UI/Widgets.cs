// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hugr.UI
{
    /// <summary>
    /// What every Hugr widget does to a clone of a vanilla one. Cloning inherits the game's look
    /// for free; it also inherits behaviour that has to go.
    /// </summary>
    internal static class Widgets
    {
        /// <summary>
        /// Removes every game script from a clone and keeps only the Unity UI machinery. A cloned
        /// widget otherwise drags along behaviours that still believe in the hierarchy they were
        /// built for: a settings page that writes into the game's options, a localizer that
        /// rewrites the caption, a gamepad handler that registers our button as one of the
        /// vanilla ones.
        /// </summary>
        internal static void StripScripts(GameObject clone)
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
        /// <see cref="UnityEngine.UI.Toggle"/>, and dropping it would leave a row that no longer
        /// toggles anything. Only behaviours that go straight to <see cref="MonoBehaviour"/> are
        /// game logic.
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
        /// Renames every caption of a widget. Valheim keeps a second copy of a tab button's label
        /// inside its "Selected" child, and that one is the only one visible once the tab is open.
        /// </summary>
        internal static void SetLabel(GameObject widget, string text, string errorCode)
        {
            TMP_Text[] labels = widget.GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length == 0)
            {
                throw new HugrException(
                    errorCode, "No text component found on " + widget.name + ", it carries " + Describe(widget) + ".");
            }

            foreach (TMP_Text label in labels)
            {
                label.text = text;
            }
        }

        /// <summary>Names what a clone is made of, so a broken graft says so in one log line.</summary>
        internal static string Describe(GameObject clone)
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
    }
}
