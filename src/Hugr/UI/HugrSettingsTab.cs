// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace Hugr.UI
{
    /// <summary>
    /// Backing behaviour of the Hugr page inside Valheim's settings panel. Implements the
    /// vanilla <see cref="ISettingsTab"/> contract so the panel drives it like its own tabs:
    /// values load when the panel opens, OK commits them, Back discards them.
    /// </summary>
    /// <remarks>
    /// Every entry point is sealed off: <c>Settings</c> drives its tabs in a loop and counts the
    /// saves, so an exception escaping from here would stop the loop and leave the panel open
    /// with no way out. Hugr reports the failure and lets the panel carry on.
    /// </remarks>
    internal class HugrSettingsTab : MonoBehaviour, ISettingsTab
    {
        private readonly List<Binding> _bindings = new List<Binding>();

#pragma warning disable 67 // Required by ISettingsTab, Hugr has no shared settings to broadcast.
        public event Action<string, int> SharedSettingChanged;
#pragma warning restore 67

        internal void Add(Toggle toggle, ConfigEntry<bool> entry)
        {
            _bindings.Add(new Binding(toggle, entry));
        }

        public void Initialize()
        {
            LoadFromConfig();
        }

        public void OnTabOpen(Button okButton, Button backButton)
        {
            LoadFromConfig();
        }

        /// <summary>Commits the toggles, then tells the panel this tab is done saving.</summary>
        public void OnOkAsync(OkActionCompletedHandler onCompleted)
        {
            try
            {
                foreach (Binding binding in _bindings)
                {
                    if (binding.Toggle != null)
                    {
                        binding.Entry.Value = binding.Toggle.isOn;
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.Report(ErrorCodes.UiSaveFailed, "Hugr settings were not saved", exception);
            }
            finally
            {
                // Settings counts this call to know when every tab is done: skipping it would
                // hang the OK button for good.
                onCompleted?.Invoke();
            }
        }

        public void OnBack()
        {
            LoadFromConfig();
        }

        public void Terminate()
        {
        }

        public void OnSharedSettingChanged(string settingName, int value)
        {
        }

        private void LoadFromConfig()
        {
            try
            {
                foreach (Binding binding in _bindings)
                {
                    if (binding.Toggle != null)
                    {
                        binding.Toggle.isOn = binding.Entry.Value;
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.Report(ErrorCodes.UiLoadFailed, "Hugr settings were not loaded", exception);
            }
        }

        private readonly struct Binding
        {
            internal Binding(Toggle toggle, ConfigEntry<bool> entry)
            {
                Toggle = toggle;
                Entry = entry;
            }

            internal Toggle Toggle { get; }

            internal ConfigEntry<bool> Entry { get; }
        }
    }
}
