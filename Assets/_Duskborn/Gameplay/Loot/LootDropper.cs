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

        private EnemyBase    _enemy;
        private ResourceNode _node;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _node  = GetComponent<ResourceNode>();
        }

        // OnEnable/OnDisable instead of Start/OnDestroy so pooled enemies correctly
        // resubscribe after SetActive(false) → SetActive(true). EnemyBase.ResetEnemy()
        // nulls OnDied, so re-subscribing on re-enable is required.
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

        private void OnEnemyDied(EnemyBase _) => TriggerDrop();

        private void TriggerDrop()
        {
            if (lootTable == null) return;
            // LootManager is server-only; Instance is null on clients, so no double-spawning.
            PlayerStats harvester = _node != null ? _node.LastHarvester : null;
            TargetType nodeTypes = _node != null ? _node.Types : TargetType.None;
            LootManager.Instance?.ServerDropLoot(lootTable, GetDropPosition(), harvester, nodeTypes);
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
