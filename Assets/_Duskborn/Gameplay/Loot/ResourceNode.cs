using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Effects;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceNode : NetworkBehaviour, IDamageable, IHealthProvider, ITypedTarget
    {
        [SerializeField] private float maxHP = 30f;

        [Header("Material Type")]
        [SerializeField, TargetTypeFilter(TargetTypeMasks.NodeTypes)]
        private TargetType materialTypes;
        public TargetType Types => materialTypes;

        [Header("Damage Numbers")]
        [SerializeField] private DamageNumberConfig _damageNumberConfig;

        [Header("Áudio")]
        [SerializeField] private AudioClip depletedClip;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private readonly SyncVar<float> _currentHP = new();
        private HitFlash _hitFlash;
        private uint _outlineMask;
        private uint _baseMask;
        private PlayerStats _lastHarvester;

        public float CurrentHP => _currentHP.Value;
        public float MaxHP     => maxHP;
        public bool  IsAlive   => _currentHP.Value > 0f;
        public PlayerStats LastHarvester => _lastHarvester;

        public event Action<float, float> OnHealthChanged;
        public event Action              OnDepleted;
        public event Action<PlayerStats> OnDepletedByHarvester;

        private void Awake()
        {
            _currentHP.Value    = maxHP;
            _currentHP.OnChange += OnHPChanged;

            _hitFlash = GetComponent<HitFlash>();
            if (_hitFlash == null) _hitFlash = gameObject.AddComponent<HitFlash>();

            if (outlineRenderer == null)
                outlineRenderer = GetComponentInChildren<Renderer>();

            if (outlineRenderer != null)
            {
                int layerIndex = RenderingLayerMask.NameToRenderingLayer(outlineLayerName);
                _outlineMask = layerIndex >= 0 ? (uint)(1 << layerIndex) : 0u;
                _baseMask    = outlineRenderer.renderingLayerMask & ~_outlineMask;
                outlineRenderer.renderingLayerMask = _baseMask;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _currentHP.Value = maxHP;
        }

        private void OnHPChanged(float prev, float next, bool asServer)
        {
            OnHealthChanged?.Invoke(next, maxHP);
        }

        public string GetSurfaceTag()
        {
            if (materialTypes.HasFlag(TargetType.Ore))
                return "Metal";
            if (materialTypes.HasFlag(TargetType.Tree) || materialTypes.HasFlag(TargetType.Bush))
                return "Tree";
            if (materialTypes.HasFlag(TargetType.MiningNode) || materialTypes.HasFlag(TargetType.Stone))
            {
                if (name.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Metal";
                return "Stone";
            }
            if (name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("fiber", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Tree";
            return "Default";
        }

        public void TakeDamage(float amount, bool isCrit = false) => TakeDamage(amount, null, isCrit);

        public void TakeDamage(float amount, PlayerStats harvester, bool isCrit = false)
        {
            if (!IsServerStarted || !IsAlive) return;
            if (harvester != null) _lastHarvester = harvester;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - amount);
            RpcShowDamageNumber(transform.position, amount, isCrit);
            DuskLog.Log(LogChannel.Loot, $"{name}: -{amount:F1} HP → {_currentHP.Value:F1}/{maxHP}");
            if (_currentHP.Value <= 0f)
            {
                OnDepleted?.Invoke();
                OnDepletedByHarvester?.Invoke(_lastHarvester);
                RpcPlayDepletedAudio(transform.position, materialTypes, GetSurfaceTag());
            }
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcPlayDepletedAudio(Vector3 pos, TargetType type, string surfaceTag)
        {
            AudioClip clip = depletedClip;
            float volume = 1.0f;
            float minDistance = 2f;
            float maxDistance = 45f;

            if (Duskborn.Audio.AudioDatabase.Instance != null)
            {
                if (clip == null)
                    clip = Duskborn.Audio.AudioDatabase.Instance.GetDepletedClip(type, surfaceTag);

                volume = Duskborn.Audio.AudioDatabase.Instance.ResourcesSettings.depletedVolume;
                minDistance = Duskborn.Audio.AudioDatabase.Instance.ResourcesSettings.minDistance;
                maxDistance = Duskborn.Audio.AudioDatabase.Instance.ResourcesSettings.maxDistance;
            }
            else if (clip == null)
            {
                bool v2 = UnityEngine.Random.value < 0.5f;
                if (surfaceTag == "Metal" || type.HasFlag(TargetType.Ore))
                    clip = Resources.Load<AudioClip>(v2 ? "SFX/ore_shatter_02" : "SFX/ore_shatter") ?? Resources.Load<AudioClip>("SFX/ore_shatter");
                else if (surfaceTag == "Tree" || type.HasFlag(TargetType.Tree))
                    clip = Resources.Load<AudioClip>(v2 ? "SFX/tree_fall_02" : "SFX/tree_fall") ?? Resources.Load<AudioClip>("SFX/tree_fall");
                else
                    clip = Resources.Load<AudioClip>(v2 ? "SFX/rock_shatter_02" : "SFX/rock_shatter") ?? Resources.Load<AudioClip>("SFX/rock_shatter");
            }

            if (clip != null)
            {
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayAtPoint(clip, pos, volume, minDistance, maxDistance);
                else
                    AudioSource.PlayClipAtPoint(clip, pos);
            }
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcShowDamageNumber(Vector3 pos, float amount, bool isCrit)
        {
            DamageNumberPool.Instance?.Get(pos, amount, isCrit, _damageNumberConfig);
            if (_hitFlash != null) _hitFlash.Flash();
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }
    }
}
