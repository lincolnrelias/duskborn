using Duskborn.Core;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [RequireComponent(typeof(PlayerBuffContainer))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerWeaponHandler : MonoBehaviour
    {
        [SerializeField] private Transform holdPoint;

        private const float DebounceSeconds = 0.5f;

        public WeaponItem ActiveWeapon => _activeWeapon;

        private PlayerStats         _stats;
        private PlayerBuffContainer _buffs;
        private ActionBarService    _actionBar;
        private bool                _subscribed;

        private WeaponItem  _activeWeapon;  // currently applied and held weapon
        private WeaponItem  _pendingWeapon; // waiting for debounce to commit
        private float       _debounceTimer;
        private GameObject  _heldInstance;  // spawned weapon prefab under holdPoint

        private void Start()
        {
            _stats = GetComponent<PlayerStats>();
            _buffs = GetComponent<PlayerBuffContainer>();
            _buffs.RegisterWeaponContributor(ApplyWeaponStats);
        }

        private void Update()
        {
            if (!_subscribed) TryCacheActionBar();

            if (_debounceTimer > 0f)
            {
                _debounceTimer -= Time.deltaTime;
                if (_debounceTimer <= 0f)
                    CommitWeaponSwap();
            }
        }

        private void OnDestroy()
        {
            if (_actionBar != null)
            {
                _actionBar.SelectedSlotChanged     -= OnSelectionChanged;
                _actionBar.Service.InventoryChanged -= OnInventoryChanged;
            }
            if (_heldInstance != null) Destroy(_heldInstance);
        }

        private void TryCacheActionBar()
        {
            var installer = FindAnyObjectByType<ActionBarInstaller>();
            if (installer?.Service == null) return;

            _actionBar = installer.Service;
            _actionBar.SelectedSlotChanged     += OnSelectionChanged;
            _actionBar.Service.InventoryChanged += OnInventoryChanged;
            _subscribed = true;
            ScheduleWeaponSwap();
        }

        private void OnSelectionChanged(int _) => ScheduleWeaponSwap();
        private void OnInventoryChanged()      => ScheduleWeaponSwap();

        private void ScheduleWeaponSwap()
        {
            var candidate = _actionBar?.Service.GetItem(_actionBar.SelectedIndex) as WeaponItem;

            if (candidate == _activeWeapon)
            {
                // Re-selected the active weapon — cancel any pending swap.
                _pendingWeapon = _activeWeapon;
                _debounceTimer = 0f;
                return;
            }

            _pendingWeapon = candidate;
            _debounceTimer = DebounceSeconds;
            DuskLog.Log(LogChannel.Inventory,
                candidate != null
                    ? $"Weapon swap pending: '{candidate.DisplayName}' ({DebounceSeconds}s)"
                    : $"Weapon deselect pending ({DebounceSeconds}s)");
        }

        private void CommitWeaponSwap()
        {
            _activeWeapon = _pendingWeapon;

            if (_heldInstance != null)
            {
                Destroy(_heldInstance);
                _heldInstance = null;
            }

            if (_activeWeapon?.Prefab != null && holdPoint != null)
                _heldInstance = Instantiate(_activeWeapon.Prefab, holdPoint);

            DuskLog.Log(LogChannel.Inventory,
                _activeWeapon != null
                    ? $"Active weapon: '{_activeWeapon.DisplayName}'"
                    : "Active weapon: none");

            _buffs.ApplyAll();
        }

        // Called by PlayerBuffContainer.ApplyAll() between gear and buffs.
        // Stats are already reset — only add here, never reset.
        private void ApplyWeaponStats()
        {
            if (_activeWeapon == null) return;
            foreach (var bonus in _activeWeapon.Bonuses)
                ApplyBonus(bonus);
        }

        private void ApplyBonus(StatBonus bonus)
        {
            switch (bonus.Type)
            {
                case StatType.HP:
                    _stats.HPMultiplier += bonus.Value;
                    break;
                case StatType.Damage:
                    _stats.DamageMultiplier += bonus.Value;
                    break;
                case StatType.MoveSpeed:
                    _stats.MoveSpeedMultiplier += bonus.Value;
                    break;
                case StatType.AttackSpeed:
                    _stats.AttackSpeedMultiplier += bonus.Value;
                    break;
                case StatType.CritChance:
                    _stats.CritChanceBonus += bonus.Value;
                    break;
                case StatType.DamageReduction:
                    _stats.IncomingDamageMultiplier =
                        Mathf.Max(0.1f, _stats.IncomingDamageMultiplier - bonus.Value);
                    break;
                default:
                    DuskLog.Warn(LogChannel.Inventory, $"Unhandled StatType: {bonus.Type}");
                    break;
            }
        }
    }
}
