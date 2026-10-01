// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Hugr.Features
{
    /// <summary>
    /// Hands a chest over to this client before a feature changes its content, through the vanilla
    /// "stack all" request: the client asks, the chest's owner checks access and use and hands the
    /// chest over. The answer to a Hugr request is taken over — vanilla would stack into the chest —
    /// and the feature runs once the chest's world data says it is ours, so its content is the one
    /// the previous owner handed over, not a copy up to a second old.
    /// Execution: client. Persistence: the chests themselves, through the vanilla save.
    /// Server interaction: vanilla RPCs only (<c>RPC_RequestStack</c> / <c>RPC_StackResponse</c>),
    /// which a vanilla server relays like any other chest request.
    /// </summary>
    internal static class ContainerClaims
    {
        /// <summary>How long a request waits for its answer and for the chest's world data.</summary>
        private const float Timeout = 5f;

        /// <summary>Chests asked, with the time: a late answer is still swallowed, then forgotten.</summary>
        private static readonly Dictionary<Container, float> Asked = new Dictionary<Container, float>();

        private static readonly List<Claim> Pending = new List<Claim>();

        /// <summary>Every container loaded, registered as it wakes up: nothing to scan for.</summary>
        private static readonly HashSet<Container> Known = new HashSet<Container>();

        /// <summary>Whether <see cref="Known"/> holds the containers loaded before the patch was on.</summary>
        private static bool _seeded;

        private static int _tickedFrame = -1;

        /// <summary>Installs the answer takeover while any of the features using it is on.</summary>
        internal static void Bind(Harmony harmony, params ConfigEntry<bool>[] users)
        {
            FeatureSwitch.BindAny(
                harmony,
                users,
                AccessTools.Method(typeof(Container), "RPC_StackResponse"),
                AccessTools.Method(typeof(ContainerClaims), nameof(OnStackResponse)),
                prefix: true);

            FeatureSwitch.BindAny(
                harmony,
                users,
                AccessTools.Method(typeof(Container), "Awake"),
                AccessTools.Method(typeof(ContainerClaims), nameof(Register)));

            // Containers woken while every user was off were missed: scan again on the next use.
            foreach (ConfigEntry<bool> user in users)
            {
                user.SettingChanged += (sender, args) => _seeded = false;
            }
        }

        /// <summary>
        /// Runs <paramref name="onOwned"/> once this client owns the chest and has its content
        /// loaded — at once for a chest already ours — or <paramref name="onFailed"/> when the owner
        /// refuses or does not answer in time. Callers drive <see cref="Tick"/>.
        /// </summary>
        internal static void Request(Container container, Action<Container> onOwned, Action onFailed)
        {
            if (container.IsOwner())
            {
                onOwned(container);
                return;
            }

            Pending.Add(new Claim(container, onOwned, onFailed, Time.time + Timeout));
            Asked[container] = Time.time;
            container.StackAll();
        }

        /// <summary>
        /// Keeps only the chests a player built and this player may use now: the owner refuses the
        /// others anyway, with a "$msg_inuse" each, and a dungeon chest or a tombstone is never
        /// touched. The content is the client copy vanilla reloads every second from the world data.
        /// </summary>
        internal static bool Usable(Container container, long playerId)
        {
            ZNetView view = container.m_nview;
            return view != null && view.IsValid() && container.GetInventory() != null
                && container.GetComponent<TombStone>() == null
                && PlacedByPlayer(container)
                && container.CheckAccess(playerId)
                && (!container.m_checkGuardStone || PrivateArea.CheckAccess(container.transform.position, 0f, false))
                && (view.IsOwner() || view.GetZDO().GetInt(ZDOVars.s_inUse) != 1);
        }

        /// <summary>The usable chests within <paramref name="range"/> of any of the centres.</summary>
        internal static List<Container> Nearby(IList<Vector3> centres, float range)
        {
            if (!_seeded)
            {
                Known.UnionWith(UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None));
                _seeded = true;
            }

            // Container has no OnDestroy to hook: a destroyed one reads as null and leaves here.
            Known.RemoveWhere(container => container == null);

            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
            List<Container> found = new List<Container>();
            foreach (Container container in Known)
            {
                Vector3 position = container.transform.position;
                foreach (Vector3 centre in centres)
                {
                    if (Vector3.Distance(position, centre) <= range)
                    {
                        if (Usable(container, playerId))
                        {
                            found.Add(container);
                        }

                        break;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Runs the claims whose chest is now ours and fails the late ones. Every feature using the
        /// claims calls it from its own update; it only works once per frame.
        /// </summary>
        internal static void Tick()
        {
            if (_tickedFrame == Time.frameCount)
            {
                return;
            }

            _tickedFrame = Time.frameCount;

            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                Claim claim = Pending[i];
                bool alive = claim.Container != null && claim.Container.m_nview.IsValid();
                if (alive && claim.Granted && claim.Container.IsOwner())
                {
                    Pending.RemoveAt(i);
                    claim.Container.Load();
                    claim.OnOwned(claim.Container);
                }
                else if (!alive || Time.time > claim.Deadline)
                {
                    Pending.RemoveAt(i);
                    Plugin.Log.LogWarning(
                        "HUGR-CLAIM-001: a chest did not answer or hand over its content in time, left untouched.");
                    claim.OnFailed();
                }
            }

            if (Asked.Count > 0)
            {
                List<Container> stale = new List<Container>();
                foreach (KeyValuePair<Container, float> asked in Asked)
                {
                    if (asked.Key == null || Time.time > asked.Value + 2f * Timeout)
                    {
                        stale.Add(asked.Key);
                    }
                }

                stale.ForEach(container => Asked.Remove(container));
            }
        }

        /// <summary>
        /// Takes over the answer when Hugr asked; the player's own "stack all" on a chest keeps its
        /// vanilla behaviour. A refusal fails the claim: that chest simply takes no part.
        /// </summary>
        private static bool OnStackResponse(Container __instance, bool granted)
        {
            if (!Asked.Remove(__instance))
            {
                return true;
            }

            Claim claim = Pending.Find(pending => pending.Container == __instance && !pending.Granted);
            if (claim == null)
            {
                return false;
            }

            if (granted)
            {
                claim.Granted = true;
            }
            else
            {
                Pending.Remove(claim);
                claim.OnFailed();
            }

            return false;
        }

        private static void Register(Container __instance)
        {
            Known.Add(__instance);
        }

        /// <summary>The piece sits on the root of a cart or a ship, above its container.</summary>
        private static bool PlacedByPlayer(Container container)
        {
            Piece piece = container.GetComponentInParent<Piece>();
            return piece != null && piece.IsPlacedByPlayer();
        }

        private sealed class Claim
        {
            internal Claim(Container container, Action<Container> onOwned, Action onFailed, float deadline)
            {
                Container = container;
                OnOwned = onOwned;
                OnFailed = onFailed;
                Deadline = deadline;
            }

            internal Container Container { get; }

            internal Action<Container> OnOwned { get; }

            internal Action OnFailed { get; }

            internal float Deadline { get; }

            internal bool Granted { get; set; }
        }
    }
}
