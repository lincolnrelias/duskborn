using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Effects;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceNode : NetworkBehaviour, IDamageable, IHealthProvider, ITypedTarget
    {
        [SerializeField] private float maxHP = 30f;

        [Header("Material Type & Tier")]
        [SerializeField, TargetTypeFilter(TargetTypeMasks.NodeTypes)]
        private TargetType materialTypes;
        public TargetType Types => materialTypes;

        [SerializeField] private CraftingTier requiredHarvestTier = CraftingTier.Primitive;
        public CraftingTier RequiredHarvestTier => requiredHarvestTier;

        [Header("Damage Numbers")]
        [SerializeField] private DamageNumberConfig _damageNumberConfig;

        [Header("Audio")]
        [SerializeField] private AudioClip depletedClip;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private readonly SyncVar<float> _currentHP = new();
        private HitFlash _hitFlash;
        private Collider _hitCollider;
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

            _hitCollider = GetComponentInChildren<Collider>();
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

        private void OnDestroy()
        {
            _currentHP.OnChange -= OnHPChanged;
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

        public string GetAudioMaterial()
        {
            var crystal = GetComponentInChildren<ElementalCrystalNodeVisual>(true);
            if (crystal != null && crystal.Element != Duskborn.Gameplay.Enchanting.RuneKind.None)
                return "Crystal_" + Duskborn.Gameplay.Enchanting.ElementalCrystalCatalog.Element(crystal.Element);
            // Cached generated scene nodes may predate the elemental visual component.
            foreach (string element in new[] { "flame", "nature", "storm", "earth", "frost", "blood", "dark", "light" })
                if (name.StartsWith("Node_" + element + "_Crystal", StringComparison.OrdinalIgnoreCase))
                    return "Crystal_" + element;
            return GetSurfaceTag();
        }

        public AudioClip ResolveDepletedClip(string audioMaterial)
        {
            if (audioMaterial.StartsWith("Crystal_", StringComparison.Ordinal))
                return Duskborn.Audio.AudioDatabase.Instance?.GetDepletedClip(materialTypes, audioMaterial);
            return depletedClip != null ? depletedClip
                : Duskborn.Audio.AudioDatabase.Instance?.GetDepletedClip(materialTypes, audioMaterial);
        }

        public void TakeDamage(float amount, bool isCrit = false) => TakeDamage(amount, null, isCrit);

        public void TakeDamage(float amount, PlayerStats harvester, bool isCrit = false, bool ownerAudioHandled = false)
        {
            if (!IsServerStarted || !IsAlive || amount <= 0f) return;
            if (harvester != null) _lastHarvester = harvester;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - amount);
            Vector3 attacker = harvester != null ? harvester.transform.position + Vector3.up * .85f
                : transform.position + Vector3.up * .8f + transform.forward * 2f;
            Vector3 contact = GetHitPosition(attacker);
            RpcPresentHit(contact, attacker - contact, GetSurfaceTag(), GetAudioMaterial(), materialTypes.HasFlag(TargetType.Bush),
                _currentHP.Value <= 0f, harvester != null ? harvester.NetworkObject : null, ownerAudioHandled);
            RpcShowDamageNumber(contact, amount, isCrit);
            DuskLog.Log(LogChannel.Loot, $"{name}: -{amount:F1} HP → {_currentHP.Value:F1}/{maxHP}");
            if (_currentHP.Value <= 0f)
            {
                OnDepleted?.Invoke();
                OnDepletedByHarvester?.Invoke(_lastHarvester);
                RpcPlayDepletedAudio(transform.position, materialTypes, GetAudioMaterial());
            }
        }

        public Vector3 GetHitPosition(Vector3 attackerPosition)
        {
            if (_hitCollider == null) _hitCollider = GetComponentInChildren<Collider>();
            return _hitCollider != null ? _hitCollider.ClosestPoint(attackerPosition)
                : transform.position + Vector3.up * .8f;
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcPresentHit(Vector3 point, Vector3 outward, string surfaceTag, string audioMaterial, bool foliage, bool depleted,
            NetworkObject harvester, bool ownerAudioHandled)
        {
            if (!IsClientStarted) return;
            // The harvesting player receives audio through their confirmed-hit TargetRpc.
            // Other damage sources and nearby observers use this node broadcast.
            if (!depleted && !(ownerAudioHandled && harvester != null && harvester.IsOwner))
                PlayHarvestHit(point, audioMaterial, foliage, false);
            ResourceHitFeedback.Emit(point, outward, surfaceTag, foliage, depleted);
        }

        public static void PlayHarvestHit(Vector3 point, string surfaceTag, bool foliage, bool localHarvester)
        {
            var settings = Duskborn.Audio.AudioDatabase.Instance?.ResourcesSettings;
            AudioClip clip = settings?.GetHitClip(surfaceTag, foliage);
            if (clip == null)
            {
                DuskLog.Warn(LogChannel.Audio, $"Harvest hit: missing {surfaceTag} recording (foliage={foliage}).");
                return;
            }
            float volume = (settings != null ? settings.hitVolume : 1f) * UnityEngine.Random.Range(.96f, 1f);
            if (Duskborn.Audio.AudioManager.Instance != null)
                Duskborn.Audio.AudioManager.Instance.PlayAtPoint(clip, point, volume, 8f, 35f, .025f,
                    localHarvester ? .2f : 1f);
            else AudioSource.PlayClipAtPoint(clip, point, volume);
            DuskLog.Log(LogChannel.Audio, $"Harvest hit: {clip.name}, local={localHarvester}, volume={volume:F2}.");
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcPlayDepletedAudio(Vector3 pos, TargetType type, string surfaceTag)
        {
            AudioClip clip = ResolveDepletedClip(surfaceTag);
            float volume = 1.0f;
            float minDistance = 2f;
            float maxDistance = 45f;
            if (surfaceTag.StartsWith("Crystal_", StringComparison.Ordinal) && clip == null)
            {
                Debug.LogError($"[CrystalAudio] Missing destruction recording for {name}: {surfaceTag}.");
                return;
            }

            if (Duskborn.Audio.AudioDatabase.Instance != null)
            {
                volume = Duskborn.Audio.AudioDatabase.Instance.ResourcesSettings.depletedVolume;
                minDistance = Mathf.Max(8f, Duskborn.Audio.AudioDatabase.Instance.ResourcesSettings.minDistance);
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
                if (surfaceTag.StartsWith("Crystal_", StringComparison.Ordinal))
                    Debug.Log($"[CrystalAudio] {name}: {surfaceTag} -> {clip.name} ({clip.length:F3}s)");
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayAtPoint(clip, pos, volume, minDistance, maxDistance, .025f, .35f);
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
