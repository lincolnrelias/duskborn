using FishNet;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Installed by WaveManager; owns only the night-three encounter.</summary>
    public sealed class HollowWardenNight : MonoBehaviour
    {
        public const string ResourcePath = "Bosses/HollowWarden";
        private HollowWardenBoss _boss;
        private bool _active;
        private bool _defeated;
        private float _finishAt;

        public bool TryBegin()
        {
            if (!InstanceFinder.IsServerStarted) return false;
            if (_active) return true;
            var cycle = DayNightCycle.Instance;
            var prefab = Resources.Load<HollowWardenBoss>(ResourcePath);
            if (cycle == null || !cycle.IsNight || cycle.CurrentNight != 3) return false;
            if (prefab == null || !FindSpawn(out var position))
            {
                Debug.LogWarning("[HollowWarden] Missing boss prefab or reachable spawn; falling back to normal night progression.");
                return false;
            }
            if (!cycle.TryHoldEncounterNight(3)) return false;
            try
            {
                _boss = Instantiate(prefab, position, Quaternion.identity);
                _boss.ResetEnemy(position);
                _boss.ApplyPlayerCountScaling(Mathf.Max(1, PlayerRegistry.AliveCount));
                _boss.OnDied += OnDefeated;
                _active = true;
                _defeated = false;
                InstanceFinder.ServerManager.Spawn(_boss.NetworkObject);
                if (!_boss.IsSpawned) { Cancel(); return false; }
                return true;
            }
            catch
            {
                Cancel();
                throw;
            }
        }

        private static bool FindSpawn(out Vector3 position)
        {
            position = default;
            var target = PlayerRegistry.FindNearest(Vector3.zero);
            if (target == null || !NavMesh.SamplePosition(target.position, out var origin, 3, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            int obstacles = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Player")) | (1 << 2));
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = attempt * Mathf.PI * 2 / 16;
                Vector3 desired = origin.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 20;
                if (!NavMesh.SamplePosition(desired, out var sample, 4, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(origin.position, sample.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (Physics.CheckCapsule(sample.position + Vector3.up * 1.2f, sample.position + Vector3.up * 3, 1, obstacles, QueryTriggerInteraction.Ignore)) continue;
                position = sample.position;
                return true;
            }
            return false;
        }

        private void OnDefeated(EnemyBase enemy)
        {
            if (!_active || _defeated) return;
            _defeated = true;
            _finishAt = Time.time + HollowWardenBoss.DeathSeconds;
            GoldManager.Instance?.AddGold(_boss.RewardGold);
            var heart = Resources.Load<InventorySystem.Data.MaterialDefinition>("Bosses/Items/material_hollow_heart");
            LootManager.Instance.ServerDropItem(heart, _boss.transform.position + Vector3.up);
            // Resume the remaining night, including ordinary waves; never force dawn on death.
            DayNightCycle.Instance?.ReleaseEncounterNight(3);
        }

        private void Update()
        {
            if (!_active || !InstanceFinder.IsServerStarted) return;
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Running)
            {
                if (GameStateManager.Instance.CurrentState != GameState.GameOver) Cancel();
                return;
            }
            if (_defeated)
            {
                if (Time.time >= _finishAt)
                {
                    _active = false;
                    Cancel();
                }
            }
            else if (_boss == null || !_boss.IsSpawned) Cancel();
        }

        public void Cancel()
        {
            DayNightCycle.Instance?.ReleaseEncounterNight(3);
            _active = _defeated = false;
            if (_boss != null)
            {
                _boss.OnDied -= OnDefeated;
                if (_boss.IsSpawned && InstanceFinder.IsServerStarted)
                    InstanceFinder.ServerManager.Despawn(_boss.NetworkObject);
                else if (!_boss.IsSpawned) Destroy(_boss.gameObject);
            }
            _boss = null;
        }

        private void OnDestroy() => Cancel();
    }
}
