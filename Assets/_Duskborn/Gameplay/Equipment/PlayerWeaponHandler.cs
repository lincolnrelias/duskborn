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
        [SerializeField] private Animator  animator;

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
            var combat = GetComponent<PlayerCombat>();
            if (combat != null && !combat.IsOwner) return;
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
            if (_activeWeapon?.Behaviour is RangedWeaponBehaviour)
                GetComponent<PlayerCombat>()?.CancelRangedAttack(true);
            var actionPlayer = GetComponent<WeaponActionPlayer>();
            if (actionPlayer != null && actionPlayer.CurrentWeapon?.Behaviour is RangedWeaponBehaviour)
                actionPlayer.CancelAction();
            _debounceTimer = DebounceSeconds;
            DuskLog.Log(LogChannel.Inventory,
                candidate != null
                    ? $"Weapon swap pending: '{candidate.DisplayName}' ({DebounceSeconds}s)"
                    : $"Weapon deselect pending ({DebounceSeconds}s)");
        }

        private void CommitWeaponSwap()
        {
            _activeWeapon = _pendingWeapon;
            GetComponent<WeaponActionPlayer>()?.SetEquippedWeapon(_activeWeapon);

            if (_heldInstance != null)
            {
                Destroy(_heldInstance);
                _heldInstance = null;
            }

            if (_activeWeapon?.Prefab != null)
            {
                Transform socket = holdPoint;
                var profile = _activeWeapon.AttachmentProfile;
                if (profile != null)
                {
                    var anim = animator != null ? animator : GetComponentInChildren<Animator>(true);
                    if (anim != null && anim.isHuman)
                    {
                        var boneTransform = IronrootAppearance.EquipmentBone(anim, profile.Bone);
                        if (boneTransform != null) socket = boneTransform;
                    }
                }

                if (socket != null)
                {
                    _heldInstance = Instantiate(_activeWeapon.Prefab, socket);
                    if (profile != null)
                    {
                        profile.ApplyToTransform(_heldInstance.transform);
                        DuskLog.Log(LogChannel.Inventory,
                            $"Weapon '{_activeWeapon.DisplayName}' attached using profile '{profile.name}' to '{socket.name}' (Pos: {_heldInstance.transform.localPosition}, Rot: {_heldInstance.transform.localEulerAngles}, Scale: {_heldInstance.transform.localScale}).");
                    }
                    else
                    {
                        DuskLog.Log(LogChannel.Inventory,
                            $"Weapon '{_activeWeapon.DisplayName}' has NO profile; attached to fallback socket '{socket.name}'.");
                    }
                }
            }

            DuskLog.Log(LogChannel.Inventory,
                _activeWeapon != null
                    ? $"Active weapon: '{_activeWeapon.DisplayName}'"
                    : "Active weapon: none");

            _buffs.ApplyAll();
        }

        // Observer weapon presentation is supplied by the shooter's network event,
        // never by another player's local action bar.
        public void ShowObservedWeapon(WeaponDefinition definition)
        {
            var combat = GetComponent<PlayerCombat>();
            if (combat == null || combat.IsOwner || definition == null) return;
            if (_activeWeapon?.Id == definition.Id) return;
            _pendingWeapon = (WeaponItem)definition.CreateRuntimeItem();
            CommitWeaponSwap();
        }

        public void ClearObservedRangedWeapon()
        {
            var combat = GetComponent<PlayerCombat>();
            if (combat == null || combat.IsOwner || !(_activeWeapon?.Behaviour is RangedWeaponBehaviour)) return;
            _pendingWeapon = null;
            CommitWeaponSwap();
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
                case StatType.MiningResourceBonus:
                    _stats.MiningResourceBonus += bonus.Value;
                    break;
                case StatType.WoodcuttingResourceBonus:
                    _stats.WoodcuttingResourceBonus += bonus.Value;
                    break;
                default:
                    DuskLog.Warn(LogChannel.Inventory, $"Unhandled StatType: {bonus.Type}");
                    break;
            }
        }
    }
}
