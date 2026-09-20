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
            foreach (Binding binding in _bindings)
            {
                binding.Entry.Value = binding.Toggle.isOn;
            }

            onCompleted?.Invoke();
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
            foreach (Binding binding in _bindings)
            {
                binding.Toggle.isOn = binding.Entry.Value;
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
