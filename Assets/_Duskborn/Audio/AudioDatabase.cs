using System;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Audio
{
    /// <summary>
    /// Banco de dados central de áudio do Duskborn.
    /// Funciona como a única fonte da verdade (Single Source of Truth) para configurações,
    /// volumes, distâncias e clipes de áudio do jogo, eliminando caminhos mágicos e hardcoded strings.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioDatabase", menuName = "Duskborn/Audio/Audio Database")]
    public class AudioDatabase : ScriptableObject
    {
        private static AudioDatabase _instance;
        public static AudioDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AudioDatabase>("Audio/AudioDatabase");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioDatabase");
                        if (guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioDatabase>(path);
                        }
                    }

                    if (_instance == null)
                    {
                        _instance = CreateDefaultAsset();
                    }
#endif
                }
                return _instance;
            }
        }

        [Header("Categorias de Áudio")]
        [SerializeField] private ResourceAudioSettings resources = new();
        [SerializeField] private PlayerAudioSettings   player    = new();
        [SerializeField] private EnemyAudioSettings    enemies   = new();
        [SerializeField] private CombatAudioSettings   combat    = new();
        [SerializeField] private LootAudioSettings     loot      = new();
        [SerializeField] private UiAudioSettings       ui        = new();
        [SerializeField] private MusicAudioSettings    music     = new();

        public ResourceAudioSettings ResourcesSettings => resources;
        public PlayerAudioSettings   Player            => player;
        public EnemyAudioSettings    Enemies           => enemies;
        public CombatAudioSettings   Combat            => combat;
        public LootAudioSettings     Loot              => loot;
        public UiAudioSettings       UI                => ui;
        public MusicAudioSettings    Music             => music;

        // ── Helpers de Resolução Rápida ───────────────────────────────────────

        public AudioClip GetDepletedClip(Gameplay.TargetType type, string surfaceTag)
        {
            return resources.GetDepletedClip(type, surfaceTag);
        }

        public AudioClip GetFootstepClip(string surfaceTag)
        {
            return player.GetFootstepClip(surfaceTag);
        }

#if UNITY_EDITOR
        public static AudioDatabase CreateDefaultAsset()
        {
            var db = CreateInstance<AudioDatabase>();
            string dir = "Assets/_Duskborn/Resources/Audio";
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            string assetPath = $"{dir}/AudioDatabase.asset";
            UnityEditor.AssetDatabase.CreateAsset(db, assetPath);
            db.AutoPopulateDefaults();
            UnityEditor.EditorUtility.SetDirty(db);
            UnityEditor.AssetDatabase.SaveAssets();
            return db;
        }

        public void AutoPopulateDefaults()
        {
            resources.AutoPopulate();
            player.AutoPopulate();
            enemies.AutoPopulate();
            combat.AutoPopulate();
            loot.AutoPopulate();
            ui.AutoPopulate();
            music.AutoPopulate();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        internal static AudioClip LoadAsset(string relativeName)
        {
#if UNITY_EDITOR
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Duskborn/Art/SFX/{relativeName}.wav")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Duskborn/Audio/Music/{relativeName}.wav")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Duskborn/Art/SFX/{relativeName}.mp3");
            if (clip != null) return clip;
#endif
            return UnityEngine.Resources.Load<AudioClip>($"SFX/{relativeName}")
                ?? UnityEngine.Resources.Load<AudioClip>($"Music/{relativeName}");
        }
    }

    // ── 1. Recursos e Coleta (Nós de Minério, Pedra e Árvores) ────────────────

    [Serializable]
    public class ResourceAudioSettings
    {
        [Header("Clipes de Destruição / Depleção")]
        public AudioClip[] rockShatterClips = Array.Empty<AudioClip>();
        public AudioClip[] oreShatterClips  = Array.Empty<AudioClip>();
        public AudioClip[] treeFallClips    = Array.Empty<AudioClip>();

        [Header("Parâmetros 3D")]
        [Range(0f, 1f)] public float depletedVolume = 1.0f;
        public float minDistance = 2f;
        public float maxDistance = 45f;

        public AudioClip GetDepletedClip(Gameplay.TargetType type, string surfaceTag)
        {
            if (string.Equals(surfaceTag, "Metal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surfaceTag, "Ore", StringComparison.OrdinalIgnoreCase) ||
                type.HasFlag(Gameplay.TargetType.Ore))
            {
                return oreShatterClips.RandomOrNull() ?? rockShatterClips.RandomOrNull();
            }

            if (string.Equals(surfaceTag, "Tree", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surfaceTag, "Wood", StringComparison.OrdinalIgnoreCase) ||
                type.HasFlag(Gameplay.TargetType.Tree))
            {
                return treeFallClips.RandomOrNull();
            }

            return rockShatterClips.RandomOrNull();
        }

        public void AutoPopulate()
        {
            if (rockShatterClips == null || rockShatterClips.Length == 0)
            {
                var c1 = AudioDatabase.LoadAsset("rock_shatter");
                var c2 = AudioDatabase.LoadAsset("rock_shatter_02");
                rockShatterClips = FilterNotNull(c1, c2);
            }

            if (oreShatterClips == null || oreShatterClips.Length == 0)
            {
                var c1 = AudioDatabase.LoadAsset("ore_shatter");
                var c2 = AudioDatabase.LoadAsset("ore_shatter_02");
                oreShatterClips = FilterNotNull(c1, c2);
            }

            if (treeFallClips == null || treeFallClips.Length == 0)
            {
                var c1 = AudioDatabase.LoadAsset("tree_fall");
                var c2 = AudioDatabase.LoadAsset("tree_fall_02");
                treeFallClips = FilterNotNull(c1, c2);
            }
        }

        private static AudioClip[] FilterNotNull(params AudioClip[] clips)
        {
            var list = new List<AudioClip>();
            foreach (var c in clips) if (c != null) list.Add(c);
            return list.ToArray();
        }
    }

    // ── 2. Jogador (Passos, Movimentação e Vitalidade) ─────────────────────────

    [Serializable]
    public class PlayerAudioSettings
    {
        [Header("Passos por Superfície")]
        public AudioClip[] grassSteps = Array.Empty<AudioClip>();
        public AudioClip[] stoneSteps = Array.Empty<AudioClip>();

        [Header("Locomoção e Salto")]
        public AudioClip jumpClip;
        public AudioClip landClip;

        [Header("Vitalidade e Combate")]
        public AudioClip[] hurtClips = Array.Empty<AudioClip>();
        public AudioClip deathClip;
        public AudioClip heartbeatLoopClip;

        [Header("Volumes e Parâmetros")]
        [Range(0f, 1f)] public float footstepVolume = 0.65f;
        [Range(0f, 1f)] public float jumpVolume     = 0.60f;
        [Range(0f, 1f)] public float landVolume     = 0.75f;
        [Range(0f, 1f)] public float hurtVolume     = 0.85f;
        [Range(0f, 1f)] public float deathVolume    = 1.0f;
        public float lowHpThreshold = 0.30f;
        public float pitchVariation = 0.08f;

        public AudioClip GetFootstepClip(string surfaceTag)
        {
            if (string.Equals(surfaceTag, "Stone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surfaceTag, "Metal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surfaceTag, "Rock", StringComparison.OrdinalIgnoreCase))
            {
                return stoneSteps.RandomOrNull();
            }
            return grassSteps.RandomOrNull();
        }

        public void AutoPopulate()
        {
            if (grassSteps == null || grassSteps.Length == 0)
            {
                grassSteps = FilterNotNull(
                    AudioDatabase.LoadAsset("footstep_grass_01"),
                    AudioDatabase.LoadAsset("footstep_grass_02"),
                    AudioDatabase.LoadAsset("footstep_grass_03"),
                    AudioDatabase.LoadAsset("footstep_grass_04")
                );
            }

            if (stoneSteps == null || stoneSteps.Length == 0)
            {
                stoneSteps = FilterNotNull(
                    AudioDatabase.LoadAsset("footstep_stone_01"),
                    AudioDatabase.LoadAsset("footstep_stone_02"),
                    AudioDatabase.LoadAsset("footstep_stone_03")
                );
            }

            if (jumpClip == null) jumpClip = AudioDatabase.LoadAsset("jump_takeoff");
            if (landClip == null) landClip = AudioDatabase.LoadAsset("jump_land");

            if (hurtClips == null || hurtClips.Length == 0)
            {
                hurtClips = FilterNotNull(
                    AudioDatabase.LoadAsset("player_hurt_01"),
                    AudioDatabase.LoadAsset("player_hurt_02")
                );
            }

            if (deathClip == null) deathClip = AudioDatabase.LoadAsset("player_death");
            if (heartbeatLoopClip == null) heartbeatLoopClip = AudioDatabase.LoadAsset("heartbeat_loop");
        }

        private static AudioClip[] FilterNotNull(params AudioClip[] clips)
        {
            var list = new List<AudioClip>();
            foreach (var c in clips) if (c != null) list.Add(c);
            return list.ToArray();
        }
    }

    // ── 3. Inimigos (Vocalizações e Archetypes) ─────────────────────────────────

    [Serializable]
    public class EnemyAudioSettings
    {
        [Header("Inimigo: Swarmer")]
        public AudioClip[] swarmerAttackClips = Array.Empty<AudioClip>();
        public AudioClip[] swarmerHurtClips   = Array.Empty<AudioClip>();
        public AudioClip[] swarmerDeathClips  = Array.Empty<AudioClip>();

        [Header("Volumes e Parâmetros")]
        [Range(0f, 1f)] public float attackVolume = 0.8f;
        [Range(0f, 1f)] public float hurtVolume   = 0.75f;
        [Range(0f, 1f)] public float deathVolume  = 0.9f;
        public float minDistance = 2f;
        public float maxDistance = 30f;

        public void AutoPopulate()
        {
            if (swarmerAttackClips == null || swarmerAttackClips.Length == 0)
            {
                swarmerAttackClips = FilterNotNull(
                    AudioDatabase.LoadAsset("enemy_swarmer_attack_01"),
                    AudioDatabase.LoadAsset("enemy_swarmer_attack_02")
                );
            }

            if (swarmerHurtClips == null || swarmerHurtClips.Length == 0)
            {
                swarmerHurtClips = FilterNotNull(
                    AudioDatabase.LoadAsset("enemy_swarmer_hurt_01"),
                    AudioDatabase.LoadAsset("enemy_swarmer_hurt_02")
                );
            }

            if (swarmerDeathClips == null || swarmerDeathClips.Length == 0)
            {
                swarmerDeathClips = FilterNotNull(
                    AudioDatabase.LoadAsset("enemy_swarmer_death_01"),
                    AudioDatabase.LoadAsset("enemy_swarmer_death_02")
                );
            }
        }

        private static AudioClip[] FilterNotNull(params AudioClip[] clips)
        {
            var list = new List<AudioClip>();
            foreach (var c in clips) if (c != null) list.Add(c);
            return list.ToArray();
        }
    }

    // ── 4. Combate e Superfícies (Fallbacks de Impacto e Perfis de Armas) ─────

    [Serializable]
    public class CombatAudioSettings
    {
        [Header("Impactos de Superfície Padrão")]
        public AudioClip[] woodHitClips    = Array.Empty<AudioClip>();
        public AudioClip[] stoneHitClips   = Array.Empty<AudioClip>();
        public AudioClip[] metalHitClips   = Array.Empty<AudioClip>();
        public AudioClip[] fleshHitClips   = Array.Empty<AudioClip>();
        public AudioClip[] defaultHitClips = Array.Empty<AudioClip>();

        [Header("Golpes no Ar (Swings)")]
        public AudioClip[] lightSwingClips = Array.Empty<AudioClip>();
        public AudioClip[] heavySwingClips = Array.Empty<AudioClip>();

        [Header("Perfis de Armas Registrados")]
        public List<WeaponAudioProfile> registeredProfiles = new();

        public AudioClip PickHitClip(string surfaceTag)
        {
            if (string.IsNullOrEmpty(surfaceTag)) surfaceTag = "Default";

            if (surfaceTag.Equals("Wood", StringComparison.OrdinalIgnoreCase) ||
                surfaceTag.Equals("Tree", StringComparison.OrdinalIgnoreCase))
                return woodHitClips.RandomOrNull();

            if (surfaceTag.Equals("Stone", StringComparison.OrdinalIgnoreCase) ||
                surfaceTag.Equals("Rock", StringComparison.OrdinalIgnoreCase))
                return stoneHitClips.RandomOrNull();

            if (surfaceTag.Equals("Metal", StringComparison.OrdinalIgnoreCase) ||
                surfaceTag.Equals("Ore", StringComparison.OrdinalIgnoreCase))
                return metalHitClips.RandomOrNull();

            if (surfaceTag.Equals("Flesh", StringComparison.OrdinalIgnoreCase) ||
                surfaceTag.Equals("Enemy", StringComparison.OrdinalIgnoreCase) ||
                surfaceTag.Equals("Player", StringComparison.OrdinalIgnoreCase))
                return fleshHitClips.RandomOrNull();

            return defaultHitClips.RandomOrNull();
        }

        public void AutoPopulate()
        {
            if (woodHitClips == null || woodHitClips.Length == 0)
                woodHitClips = FilterNotNull(
                    AudioDatabase.LoadAsset("hit_wood_01"),
                    AudioDatabase.LoadAsset("hit_wood_02"),
                    AudioDatabase.LoadAsset("hit_wood_03"),
                    AudioDatabase.LoadAsset("axe_hit_wood")
                );

            if (stoneHitClips == null || stoneHitClips.Length == 0)
                stoneHitClips = FilterNotNull(
                    AudioDatabase.LoadAsset("hit_stone_01"),
                    AudioDatabase.LoadAsset("hit_stone_02"),
                    AudioDatabase.LoadAsset("hit_stone_03")
                );

            if (metalHitClips == null || metalHitClips.Length == 0)
                metalHitClips = FilterNotNull(
                    AudioDatabase.LoadAsset("hit_metal_01"),
                    AudioDatabase.LoadAsset("hit_metal_02"),
                    AudioDatabase.LoadAsset("hit_metal_03"),
                    AudioDatabase.LoadAsset("hit_ore_01"),
                    AudioDatabase.LoadAsset("hit_ore_02"),
                    AudioDatabase.LoadAsset("hit_ore_03")
                );

            if (fleshHitClips == null || fleshHitClips.Length == 0)
                fleshHitClips = FilterNotNull(
                    AudioDatabase.LoadAsset("hit_flesh_01"),
                    AudioDatabase.LoadAsset("hit_flesh_02"),
                    AudioDatabase.LoadAsset("hit_flesh_03"),
                    AudioDatabase.LoadAsset("punch_3")
                );

            if (defaultHitClips == null || defaultHitClips.Length == 0)
                defaultHitClips = FilterNotNull(
                    AudioDatabase.LoadAsset("hit_default_01"),
                    AudioDatabase.LoadAsset("hit_default_02")
                );

            if (lightSwingClips == null || lightSwingClips.Length == 0)
                lightSwingClips = FilterNotNull(
                    AudioDatabase.LoadAsset("swing_light_01"),
                    AudioDatabase.LoadAsset("swing_light_02"),
                    AudioDatabase.LoadAsset("swing_light_03"),
                    AudioDatabase.LoadAsset("axe_swing"),
                    AudioDatabase.LoadAsset("weapon_swing_1"),
                    AudioDatabase.LoadAsset("weapon_swing_2")
                );

            if (heavySwingClips == null || heavySwingClips.Length == 0)
                heavySwingClips = FilterNotNull(
                    AudioDatabase.LoadAsset("swing_heavy_01"),
                    AudioDatabase.LoadAsset("swing_heavy_02"),
                    AudioDatabase.LoadAsset("swing_heavy_03")
                );

#if UNITY_EDITOR
            if (registeredProfiles == null || registeredProfiles.Count == 0)
            {
                registeredProfiles = new List<WeaponAudioProfile>();
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WeaponAudioProfile");
                foreach (var g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    var p = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponAudioProfile>(path);
                    if (p != null && !registeredProfiles.Contains(p)) registeredProfiles.Add(p);
                }
            }
#endif
        }

        private static AudioClip[] FilterNotNull(params AudioClip[] clips)
        {
            var list = new List<AudioClip>();
            foreach (var c in clips) if (c != null) list.Add(c);
            return list.ToArray();
        }
    }

    // ── 5. Loot e Interações (Ouro, Itens e Baús) ──────────────────────────────

    [Serializable]
    public class LootAudioSettings
    {
        [Header("Coleta e Baús")]
        public AudioClip goldPickupClip;
        public AudioClip itemPickupClip;
        public AudioClip chestOpenClip;

        [Header("Volumes")]
        [Range(0f, 1f)] public float goldVolume  = 1.0f;
        [Range(0f, 1f)] public float itemVolume  = 1.0f;
        [Range(0f, 1f)] public float chestVolume = 1.0f;

        public void AutoPopulate()
        {
            if (goldPickupClip == null)
                goldPickupClip = AudioDatabase.LoadAsset("gold_pickup");

            if (itemPickupClip == null)
                itemPickupClip = AudioDatabase.LoadAsset("item_pickup");

            if (chestOpenClip == null)
                chestOpenClip = AudioDatabase.LoadAsset("chest_open");
        }
    }

    // ── 6. Interface de Usuário (UI) ──────────────────────────────────────────

    [Serializable]
    public class UiAudioSettings
    {
        [Header("Efeitos Sonoros de Interface")]
        public AudioClip buttonClickClip;
        public AudioClip modalOpenClip;
        public AudioClip craftSuccessClip;
        public AudioClip errorClip;

        [Header("Volumes")]
        [Range(0f, 1f)] public float buttonClickVolume  = 1.0f;
        [Range(0f, 1f)] public float modalOpenVolume    = 1.0f;
        [Range(0f, 1f)] public float craftSuccessVolume = 1.0f;
        [Range(0f, 1f)] public float errorVolume        = 1.0f;

        public void AutoPopulate()
        {
            if (buttonClickClip == null) buttonClickClip = AudioDatabase.LoadAsset("ui_button_click");
            if (modalOpenClip == null)   modalOpenClip   = AudioDatabase.LoadAsset("ui_modal_open");
            if (craftSuccessClip == null) craftSuccessClip = AudioDatabase.LoadAsset("hit_stone_01");
            if (errorClip == null)       errorClip       = AudioDatabase.LoadAsset("ui_error");
        }
    }

    // ── 7. Música e Atmosfera (Ciclo Dia/Noite e Stingers) ────────────────────

    [Serializable]
    public class MusicAudioSettings
    {
        [Header("Trilhas de Música")]
        public AudioClip menuMusic;
        public AudioClip dayMusic;
        public AudioClip nightMusic;

        [Header("Stingers e Berrantes")]
        public AudioClip dawnHorn;
        public AudioClip nightHorn;

        [Header("Parâmetros")]
        public float musicFadeDuration = 2.5f;
        [Range(0f, 1f)] public float hornVolume = 0.9f;

        public void AutoPopulate()
        {
            if (menuMusic == null)  menuMusic  = AudioDatabase.LoadAsset("music_main_menu");
            if (dayMusic == null)   dayMusic   = AudioDatabase.LoadAsset("music_day_exploration");
            if (nightMusic == null) nightMusic = AudioDatabase.LoadAsset("music_night_combat");
            if (dawnHorn == null)   dawnHorn   = AudioDatabase.LoadAsset("dawn_horn");
            if (nightHorn == null)  nightHorn  = AudioDatabase.LoadAsset("night_horn");
        }
    }
}
