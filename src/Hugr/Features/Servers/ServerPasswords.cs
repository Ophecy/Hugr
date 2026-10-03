// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using HarmonyLib;
using Hugr.Configuration;

namespace Hugr.Features.Servers
{
    /// <summary>
    /// Remembers the password of every server the player joined and types it in the vanilla
    /// password dialog on the next join. A password is only kept once the server accepted it;
    /// a remembered one the server refuses is forgotten and the join is replayed, so the player
    /// is asked again.
    /// Execution: client. Persistence: client (BepInEx config, plain text). Server interaction:
    /// none beyond the vanilla handshake — Hugr answers it exactly as the keyboard would.
    /// </summary>
    internal static class ServerPasswords
    {
        private static string _pendingServer;

        private static string _pendingPassword;

        private static bool _autoFilled;

        private static bool _rejoin;

        private static ServerJoinData _lastJoin;

        internal static void Bind(Harmony harmony)
        {
            Patch(harmony, typeof(FejdStartup), nameof(FejdStartup.JoinServer), nameof(OnJoinServer));
            Patch(harmony, typeof(ZNet), nameof(ZNet.RPC_ClientHandshake), nameof(OnClientHandshake));
            Patch(harmony, typeof(ZNet), nameof(ZNet.OnPasswordEntered), nameof(OnPasswordEntered));
            Patch(harmony, typeof(ZNet), nameof(ZNet.RPC_PeerInfo), nameof(OnPeerInfo));
            Patch(harmony, typeof(ZNet), nameof(ZNet.RPC_Error), nameof(OnError));
            Patch(harmony, typeof(FejdStartup), nameof(FejdStartup.Start), nameof(OnStartupShown));
        }

        private static void Patch(Harmony harmony, Type type, string target, string postfix)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.ServerPasswords,
                AccessTools.Method(type, target),
                AccessTools.Method(typeof(ServerPasswords), postfix));
        }

        /// <summary>Keeps the join target: the menu that could replay it is destroyed on the way in.</summary>
        private static void OnJoinServer(FejdStartup __instance)
        {
            _lastJoin = __instance.m_joinServer;
        }

        /// <summary>
        /// Answers the password dialog vanilla just opened. It stays closed when vanilla answered
        /// it itself from the <c>-password</c> command line, which then takes precedence.
        /// </summary>
        private static void OnClientHandshake(ZNet __instance, bool needPassword)
        {
            Guard(() =>
            {
                _autoFilled = false;
                if (!needPassword || !__instance.m_passwordDialog.gameObject.activeSelf)
                {
                    return;
                }

                string password = ModConfig.ServerPassword(ZNet.GetServerString(true)).Value;
                if (string.IsNullOrEmpty(password))
                {
                    return;
                }

                _autoFilled = true;
                __instance.OnPasswordEntered(password);
                Plugin.Log.LogInfo("Remembered password sent to " + ZNet.GetServerString(true) + ".");
            });
        }

        private static void OnPasswordEntered(string pwd)
        {
            Guard(() =>
            {
                _pendingServer = ZNet.GetServerString(true);
                _pendingPassword = pwd;
            });
        }

        /// <summary>The server let the player in: the password it was sent is the right one.</summary>
        private static void OnPeerInfo()
        {
            Guard(() =>
            {
                if (_pendingPassword == null || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected)
                {
                    return;
                }

                ModConfig.ServerPassword(_pendingServer).Value = _pendingPassword;
                Plugin.Log.LogInfo("Password of " + _pendingServer + " remembered.");
                _pendingServer = null;
                _pendingPassword = null;
            });
        }

        private static void OnError(int error)
        {
            Guard(() =>
            {
                if (error != (int)ZNet.ConnectionStatus.ErrorPassword || !_autoFilled)
                {
                    return;
                }

                ModConfig.ServerPassword(_pendingServer).Value = string.Empty;
                Plugin.Log.LogWarning("Remembered password of " + _pendingServer + " refused, forgotten.");
                _pendingServer = null;
                _pendingPassword = null;
                _autoFilled = false;
                _rejoin = _lastJoin.IsValid;
            });
        }

        /// <summary>Back in the menu after a refused password: joins again so the player is asked.</summary>
        private static void OnStartupShown(FejdStartup __instance)
        {
            Guard(() =>
            {
                if (!_rejoin)
                {
                    return;
                }

                _rejoin = false;
                __instance.OnConnectionFailedOk();
                __instance.SetServerToJoin(_lastJoin);
                __instance.JoinServer();
            });
        }

        /// <summary>A patch never lets an exception reach the game: it reports it instead.</summary>
        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Plugin.Report(
                    ErrorCodes.ServerUnexpected, "unexpected failure while handling a server password", exception);
            }
        }
    }
}
