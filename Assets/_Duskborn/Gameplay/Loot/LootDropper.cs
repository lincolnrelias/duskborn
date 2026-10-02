using UnityEngine;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Loot
{
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] private DropLootTable lootTable;
        [SerializeField] private Transform     dropOrigin;
        [SerializeField] private float         dropOriginUpOffset = 0.5f;

        public DropLootTable LootTable
        {
            get => lootTable;
            set => lootTable = value;
        }

        public void SetLootTable(DropLootTable table) => lootTable = table;

        private EnemyBase    _enemy;
        private ResourceNode _node;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _node  = GetComponent<ResourceNode>();
        }

        // OnEnable/OnDisable instead of Start/OnDestroy so pooled enemies correctly
        // resubscribe after SetActive(false) → SetActive(true). ResetEnemy preserves
        // subscriptions established by FishNet activating the pooled object.
        private void OnEnable()
        {
            if (_enemy != null) _enemy.OnDied    += OnEnemyDied;
            if (_node  != null) _node.OnDepleted += TriggerDrop;
        }

        private void OnDisable()
        {
            if (_enemy != null) _enemy.OnDied    -= OnEnemyDied;
            if (_node  != null) _node.OnDepleted -= TriggerDrop;
        }

        private void OnEnemyDied(EnemyBase enemy) => TriggerDrop(enemy != null ? enemy.LastAttacker : null);

        private void TriggerDrop() => TriggerDrop(_node != null ? _node.LastHarvester : null);

        private void TriggerDrop(PlayerStats destroyer)
        {
            if (lootTable == null) return;
            // LootManager is server-only; Instance is null on clients, so no double-spawning.
            TargetType nodeTypes = _node != null ? _node.Types : TargetType.None;
            LootManager.Instance?.ServerDropLoot(lootTable, GetDropPosition(), destroyer, nodeTypes);
        }

        private Vector3 GetDropPosition()
        {
            Vector3 basePos = (dropOrigin != null && dropOrigin != transform)
                ? dropOrigin.position
                : transform.position;
            return basePos + Vector3.up * dropOriginUpOffset;
        }
    }
}
