using System;
using System.Collections.Generic;
using System.Linq;
using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    public partial class PlayerInteractor
    {
        public static readonly HashSet<PlayerInteractor> BuildingPeers = new();
        private BuildingCommand pendingBuilding;
        private Dictionary<string, int> pendingCost;
        private string pendingToken;
        private readonly HashSet<string> paidBuildingTokens = new();
        public bool BuildingTransactionPending => pendingBuilding != null;
        public override void OnStartServer()
        {
            base.OnStartServer(); BuildingPeers.Add(this); BuildingWorld.Ensure();
        }
        public override void OnStopServer()
        {
            BuildingPeers.Remove(this); pendingBuilding = null; pendingCost = null; pendingToken = null;
            base.OnStopServer();
        }
        private void InitializeBuildingClient()
        {
            BuildingWorld.Ensure();
            if (GetComponent<RecipeDiscoveryTracker>() == null) gameObject.AddComponent<RecipeDiscoveryTracker>();
            if (GetComponent<BuildingController>() == null) gameObject.AddComponent<BuildingController>();
            GrantStarterMaterials();
            RequestBuildingSnapshotRpc();
        }
        private void GrantStarterMaterials()
        {
            var inventory = GetComponent<ResourceInventory>();
            if (inventory == null) return;
            inventory.EnsureStartingAmount("material_stone", 50);
            inventory.EnsureStartingAmount("material_wood", 50);
            inventory.EnsureStartingAmount("material_iron", 50);
        }
        [ServerRpc] private void RequestBuildingSnapshotRpc() => SendBuildingSnapshot(JsonUtility.ToJson(BuildingWorld.Ensure().Capture()));
        public void SendBuildingSnapshot(string json) { if (IsServerStarted) BuildingSnapshotRpc(Owner, json); }
        [TargetRpc] private void BuildingSnapshotRpc(NetworkConnection connection, string json)
        {
            if (!IsServerStarted) BuildingWorld.Ensure().ApplySnapshot(JsonUtility.FromJson<BuildingSnapshot>(json));
        }
        public void RequestBuilding(BuildingCommand command)
        {
            if (IsOwner) RequestBuildingRpc(JsonUtility.ToJson(command));
        }
        [ServerRpc] private void RequestBuildingRpc(string json)
        {
            // Every client command must finish with a result. Silently ignoring a
            // duplicate or malformed request leaves the placement UI permanently
            // waiting for its transaction callback.
            if (pendingBuilding != null)
            {
                BuildingResultRpc(Owner, "Outra construção ainda está sendo confirmada.", "", "");
                return;
            }
            if (json == null || json.Length > 4096)
            {
                BuildingResultRpc(Owner, "Pedido inválido.", "", "");
                return;
            }
            BuildingCommand command;
            try { command = JsonUtility.FromJson<BuildingCommand>(json); }
            catch { BuildingResultRpc(Owner, "Pedido inválido.", "", ""); return; }
            var world = BuildingWorld.Ensure();
            var reason = world.Validate(command, this, out var costs);
            if (reason != null) { BuildingResultRpc(Owner, reason, "", ""); return; }
            if (costs.Count == 0)
            {
                var credits = world.Commit(command);
                BuildingResultRpc(Owner, "", Encode(credits), ""); world.Broadcast(); return;
            }
            pendingBuilding = command; pendingCost = costs; pendingToken = Guid.NewGuid().ToString("N");
            PayBuildingRpc(Owner, pendingToken, JsonUtility.ToJson(command), Encode(costs));
        }
        [TargetRpc] private void PayBuildingRpc(NetworkConnection connection, string token, string commandJson, string costJson)
        {
            if (!paidBuildingTokens.Add(token)) return;
            var command = JsonUtility.FromJson<BuildingCommand>(commandJson);
            bool unlocked = true;
            var discovery = GetComponent<RecipeDiscoveryTracker>();
            if (command.action == "place") unlocked = BuildingWorld.Instance.Definition(command.definition).IsUnlocked(discovery);
            if (command.action == "queue" || command.action == "load")
                unlocked = discovery != null && discovery.IsDiscovered(BuildingWorld.Recipe(command.recipe));
            bool paid = unlocked && GetComponent<ResourceInventory>().TrySpendBatch(Decode(costJson));
            CompleteBuildingRpc(token, paid);
        }
        [ServerRpc] private void CompleteBuildingRpc(string token, bool paid)
        {
            if (pendingBuilding == null || token != pendingToken) return;
            var command = pendingBuilding; var cost = pendingCost;
            pendingBuilding = null; pendingCost = null; pendingToken = null;
            if (!paid) { BuildingResultRpc(Owner, "Materiais insuficientes ou construção/receita bloqueada.", "", token); return; }
            var world = BuildingWorld.Ensure();
            var reason = world.Validate(command, this, out _);
            if (reason != null) { BuildingResultRpc(Owner, reason, Encode(cost), token); return; }
            Dictionary<string, int> credits;
            try { credits = world.Commit(command); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                BuildingResultRpc(Owner, "Falha ao construir; materiais devolvidos.", Encode(cost), token);
                return;
            }
            BuildingResultRpc(Owner, "", Encode(credits), token);
            world.Broadcast();
        }
        [TargetRpc] private void BuildingResultRpc(NetworkConnection connection, string error, string credits, string token)
        {
            if (!string.IsNullOrEmpty(token)) paidBuildingTokens.Remove(token);
            if (!string.IsNullOrEmpty(credits)) foreach (var p in Decode(credits)) GetComponent<ResourceInventory>().Add(p.Key, p.Value);
            GetComponent<BuildingController>()?.Complete(error);
        }
        private static string Encode(Dictionary<string, int> values) => JsonUtility.ToJson(new BuildingSnapshot { hostResources = values.Select(p => new MaterialStack { id = p.Key, amount = p.Value }).ToList() });
        private static Dictionary<string, int> Decode(string json) => JsonUtility.FromJson<BuildingSnapshot>(json).hostResources.ToDictionary(s => s.id, s => s.amount);
    }
}



