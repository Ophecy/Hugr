// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Hugr.Configuration;
using UnityEngine;

namespace Hugr.Features.Crafting
{
    /// <summary>
    /// Crafting and building count the chests, carts and ships near the player and near every
    /// crafting station whose range covers the player. The crafting panel and the build menu show
    /// the chests' content in their counts; the craft or the placement itself stays vanilla: what
    /// the inventory lacks is first moved into it from the chests, then the vanilla routine
    /// consumes it from the inventory — station rules, skill, effects and messages included.
    /// A chest another client owns answers within a network round trip: the craft waits for it,
    /// the placement is replayed through the vanilla place-button buffer once it arrives.
    /// Execution: client. Persistence: the chests and the inventory, through the vanilla save.
    /// Server interaction: vanilla RPCs only, through <see cref="ContainerClaims"/>.
    /// </summary>
    internal static class ContainerResources
    {
        /// <summary>The counts shown come from a snapshot this old at most.</summary>
        private const float StockLifetime = 1f;

        private static readonly Dictionary<string, int> Stock = new Dictionary<string, int>();

        private static float _stockTime = float.MinValue;

        /// <summary>Frame and nesting of the display calls whose counts include the chests.</summary>
        private static int _scopeFrame = -1;

        private static int _scopeDepth;

        private static int _placingFrame = -1;

        private static Pull _pull;

        private static bool _craftDeferred;

        internal static void Bind(Harmony harmony)
        {
            ModConfig.CraftFromContainers.SettingChanged += (sender, args) => Reset();

            Bind(harmony, typeof(InventoryGui), "Update", nameof(OnUpdate));
            Bind(harmony, typeof(Inventory), "CountItems", nameof(AddStock));

            foreach ((Type type, string method) in new[]
            {
                (typeof(InventoryGui), "UpdateRecipe"),
                (typeof(InventoryGui), "UpdateRecipeList"),
                (typeof(Hud), "UpdatePieceList"),
                (typeof(Hud), "SetupPieceInfo"),
                (typeof(BuildUiPieceButton), "UpdateRequirements"),
            })
            {
                Bind(harmony, type, method, nameof(EnterScope), prefix: true);
                Bind(harmony, type, method, nameof(ExitScope));
            }

            Bind(harmony, typeof(InventoryGui), "OnCraftPressed", nameof(OnCraftPressed));
            Bind(harmony, typeof(InventoryGui), "DoCrafting", nameof(OnDoCrafting), prefix: true);
            Bind(harmony, typeof(Player), "UpdatePlacement", nameof(EnterPlacement), prefix: true);
            Bind(harmony, typeof(Player), "UpdatePlacement", nameof(ExitPlacement));

            FeatureSwitch.Bind(
                harmony,
                ModConfig.CraftFromContainers,
                AccessTools.Method(
                    typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) }),
                AccessTools.Method(typeof(ContainerResources), nameof(OnPieceCheck)));
        }

        private static void Bind(Harmony harmony, Type type, string target, string patch, bool prefix = false)
        {
            FeatureSwitch.Bind(
                harmony,
                ModConfig.CraftFromContainers,
                AccessTools.Method(type, target),
                AccessTools.Method(typeof(ContainerResources), patch),
                prefix);
        }

        private static void OnUpdate()
        {
            try
            {
                ContainerClaims.Tick();
            }
            catch (Exception exception)
            {
                // Runs inside InventoryGui.Update: nothing may escape into the game loop.
                Plugin.Log.LogError("HUGR-CRAFT-000: unexpected failure while fetching resources (" + exception + ").");
            }
        }

        private static void EnterScope()
        {
            if (_scopeFrame != Time.frameCount)
            {
                // A scope left open by an exception dies with its frame.
                _scopeFrame = Time.frameCount;
                _scopeDepth = 0;
            }

            _scopeDepth++;
        }

        private static void ExitScope()
        {
            if (_scopeDepth > 0)
            {
                _scopeDepth--;
            }
        }

        /// <summary>Counts from here on are the inventory's only: the vanilla routine consumes next.</summary>
        private static void Strict()
        {
            _scopeDepth = 0;
        }

        private static bool InScope()
        {
            return _scopeDepth > 0 && _scopeFrame == Time.frameCount;
        }

        /// <summary>
        /// Adds the chests' content to the player's count, inside the display calls only: any other
        /// caller — the consuming routines first — keeps the vanilla count.
        /// </summary>
        private static void AddStock(Inventory __instance, string name, ref int __result)
        {
            if (!InScope())
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || __instance != player.GetInventory())
            {
                return;
            }

            if (Time.time - _stockTime > StockLifetime)
            {
                RefreshStock(player);
            }

            // ponytail: counts every quality alike, right for materials; per quality if an upgrade needs it.
            if (Stock.TryGetValue(name, out int stocked))
            {
                __result += stocked;
            }
        }

        /// <summary>
        /// Pulls what the craft lacks as soon as its timer starts: the craft duration usually
        /// covers the round trip to a chest another client owns.
        /// </summary>
        private static void OnCraftPressed(InventoryGui __instance)
        {
            try
            {
                Player player = Player.m_localPlayer;
                Recipe recipe = __instance.m_craftRecipe;
                if (__instance.m_craftTimer < 0f || _pull != null || player == null || recipe == null)
                {
                    return;
                }

                int quality = __instance.m_craftUpgradeItem != null ? __instance.m_craftUpgradeItem.m_quality + 1 : 1;
                int multiplier = __instance.m_multiCrafting ? __instance.m_multiCraftAmount : 1;
                Dictionary<string, int> missing = Missing(
                    player, recipe.m_resources, quality, multiplier, recipe.m_requireOnlyOneIngredient);
                if (missing.Count > 0)
                {
                    StartPull(player, missing, OnCraftPulled);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("HUGR-CRAFT-002: could not fetch the resources of a craft (" + exception + ").");
            }
        }

        /// <summary>The craft timer ran out before the chests answered: it resumes when they do.</summary>
        private static bool OnDoCrafting()
        {
            Strict();
            if (_pull == null)
            {
                return true;
            }

            _craftDeferred = true;
            return false;
        }

        private static void OnCraftPulled(bool complete)
        {
            if (!_craftDeferred)
            {
                return;
            }

            _craftDeferred = false;
            Player player = Player.m_localPlayer;
            if (player != null && InventoryGui.IsVisible())
            {
                // Vanilla checks the inventory again and says what is still missing, if anything.
                InventoryGui.instance.DoCrafting(player);
            }
        }

        private static void EnterPlacement(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                Strict();
                _placingFrame = Time.frameCount;
            }
        }

        private static void ExitPlacement()
        {
            _placingFrame = -1;
        }

        /// <summary>
        /// Inside <c>UpdatePlacement</c>, the only requirement check outside a display scope is the
        /// one of a placement the player just asked for — the build menu, opened from there too,
        /// checks every piece inside its own scope. A chest this client owns hands its content over
        /// within the call, and the placement goes on at once; otherwise vanilla says what is
        /// missing and the placement is replayed when the chests answer.
        /// </summary>
        private static void OnPieceCheck(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (__result || mode != Player.RequirementMode.CanBuild || _placingFrame != Time.frameCount
                || InScope() || _pull != null || __instance != Player.m_localPlayer || piece == null)
            {
                return;
            }

            try
            {
                Dictionary<string, int> missing = Missing(__instance, piece.m_resources, 0, 1, false);
                RefreshStock(__instance);
                if (missing.Count == 0 || missing.Any(lack => Stocked(lack.Key) < lack.Value))
                {
                    return;
                }

                bool pulled = false;
                StartPull(__instance, missing, complete =>
                {
                    pulled = complete;
                    if (complete)
                    {
                        Replay(piece);
                    }
                });

                if (pulled)
                {
                    __result = __instance.HaveRequirements(piece, mode);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("HUGR-CRAFT-003: could not fetch the resources of a piece (" + exception + ").");
            }
        }

        /// <summary>Presses the place button again, through the buffer vanilla keeps for 0.2 s.</summary>
        private static void Replay(Piece piece)
        {
            Player player = Player.m_localPlayer;
            if (player != null && _placingFrame != Time.frameCount && player.InPlaceMode()
                && player.GetSelectedPiece() == piece)
            {
                player.m_placePressedTime = Time.time;
            }
        }

        /// <summary>
        /// What the inventory lacks, by item name, with the filter and the amounts of
        /// <c>Player.ConsumeResources</c>: at an upgrader only the upgrader resources count,
        /// elsewhere only the others. For a recipe satisfied by any one of its ingredients, nothing
        /// when one is already there, else the first one the chests can complete.
        /// </summary>
        private static Dictionary<string, int> Missing(
            Player player, Piece.Requirement[] requirements, int quality, int multiplier, bool onlyOne)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();
            Dictionary<string, int> missing = new Dictionary<string, int>();

            foreach (Piece.Requirement requirement in requirements)
            {
                bool counted = station != null
                    ? station.m_upgrader == requirement.m_upgraderResource
                    : !requirement.m_upgraderResource;
                int need = requirement.m_resItem == null ? 0 : requirement.GetAmount(quality) * multiplier;
                if (!counted || need <= 0)
                {
                    continue;
                }

                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int lack = need - CountCarried(inventory, name);
                if (onlyOne && lack <= 0)
                {
                    return new Dictionary<string, int>();
                }

                if (lack > 0)
                {
                    missing[name] = (missing.TryGetValue(name, out int already) ? already : 0) + lack;
                }
            }

            if (onlyOne)
            {
                RefreshStock(player);
                foreach (KeyValuePair<string, int> lack in missing)
                {
                    if (Stocked(lack.Key) >= lack.Value)
                    {
                        return new Dictionary<string, int> { { lack.Key, lack.Value } };
                    }
                }

                return new Dictionary<string, int>();
            }

            return missing;
        }

        /// <summary>
        /// Moves what is missing from the chests into the inventory. A chest this client owns
        /// gives within the call, hence the request loop counting itself among the answers.
        /// </summary>
        private static void StartPull(Player player, Dictionary<string, int> missing, Action<bool> onDone)
        {
            Pull pull = new Pull(missing, onDone);
            _pull = pull;

            foreach (Container container in ContainerClaims.Nearby(Centres(player), ModConfig.CraftFromContainersRange.Value))
            {
                if (container.GetInventory().GetAllItems().Any(item => missing.ContainsKey(item.m_shared.m_name)))
                {
                    pull.Waiting++;
                    ContainerClaims.Request(container, owned => Take(pull, owned), () => Answered(pull));
                }
            }

            Answered(pull);
        }

        private static void Take(Pull pull, Container container)
        {
            Player player = Player.m_localPlayer;
            if (pull == _pull && player != null)
            {
                Inventory inventory = player.GetInventory();
                Inventory chest = container.GetInventory();
                foreach (ItemDrop.ItemData item in chest.GetAllItems().ToList())
                {
                    string name = item.m_shared.m_name;
                    if (!pull.Remaining.TryGetValue(name, out int remaining) || remaining <= 0)
                    {
                        continue;
                    }

                    int taken = Math.Min(item.m_stack, remaining);
                    ItemDrop.ItemData moved = item.Clone();
                    moved.m_stack = taken;
                    if (!inventory.CanAddItem(moved, taken))
                    {
                        pull.Full = true;
                        continue;
                    }

                    inventory.AddItem(moved);
                    chest.RemoveItem(item, taken);
                    pull.Remaining[name] = remaining - taken;
                    Plugin.Trace("Took " + taken + " " + name + " from " + container.name + ".");
                }
            }

            Answered(pull);
        }

        /// <summary>One chest answered, or the request loop ended: the last one closes the pull.</summary>
        private static void Answered(Pull pull)
        {
            if (pull != _pull || --pull.Waiting > 0)
            {
                return;
            }

            _pull = null;
            _stockTime = float.MinValue;
            bool complete = pull.Remaining.Values.All(remaining => remaining <= 0);
            Plugin.Trace("Pull " + (complete ? "complete." : "incomplete."));
            if (!complete)
            {
                Plugin.Log.LogWarning(
                    "HUGR-CRAFT-001: the nearby chests did not hand over everything that was missing"
                    + (pull.Full ? ", the inventory is full." : "."));
                if (pull.Full && Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$inventory_full");
                }
            }

            pull.OnDone(complete);
        }

        private static void RefreshStock(Player player)
        {
            Stopwatch watch = Stopwatch.StartNew();
            Stock.Clear();
            _stockTime = Time.time;
            List<Container> nearby = ContainerClaims.Nearby(Centres(player), ModConfig.CraftFromContainersRange.Value);
            foreach (Container container in nearby)
            {
                foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                {
                    string name = item.m_shared.m_name;
                    Stock[name] = (Stock.TryGetValue(name, out int count) ? count : 0) + item.m_stack;
                }
            }

            Plugin.Trace(
                "Stock refreshed in " + watch.Elapsed.TotalMilliseconds.ToString("0.000") + " ms: "
                + nearby.Count + " chest(s) in range, " + Stock.Count + " kind(s) of item.");
        }

        /// <summary>The inventory's own count, whatever scope the caller runs in.</summary>
        private static int CountCarried(Inventory inventory, string name)
        {
            int depth = _scopeDepth;
            _scopeDepth = 0;
            try
            {
                return inventory.CountItems(name);
            }
            finally
            {
                _scopeDepth = depth;
            }
        }

        private static int Stocked(string name)
        {
            return Stock.TryGetValue(name, out int count) ? count : 0;
        }

        /// <summary>The player, and every crafting station whose build range covers the player.</summary>
        private static List<Vector3> Centres(Player player)
        {
            Vector3 position = player.transform.position;
            List<Vector3> centres = new List<Vector3> { position };
            foreach (CraftingStation station in CraftingStation.m_allStations)
            {
                if (station != null
                    && Vector3.Distance(station.transform.position, position) <= station.GetStationBuildRange())
                {
                    centres.Add(station.transform.position);
                }
            }

            return centres;
        }

        /// <summary>Forgets the pull in flight: its answers are then ignored.</summary>
        private static void Reset()
        {
            _pull = null;
            _craftDeferred = false;
            _scopeDepth = 0;
            _stockTime = float.MinValue;
        }

        private sealed class Pull
        {
            internal Pull(Dictionary<string, int> missing, Action<bool> onDone)
            {
                Remaining = new Dictionary<string, int>(missing);
                OnDone = onDone;
                Waiting = 1;
            }

            internal Dictionary<string, int> Remaining { get; }

            internal Action<bool> OnDone { get; }

            internal int Waiting { get; set; }

            internal bool Full { get; set; }
        }
    }
}
