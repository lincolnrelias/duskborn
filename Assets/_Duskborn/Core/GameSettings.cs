using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Duskborn.Audio;
using Duskborn.Gameplay.Player;

namespace Duskborn.Core
{
    /// <summary>
    /// Central manager for persistent Duskborn preferences and settings.
    /// Manages volume (Master, Music, SFX, Ambient, UI), display (Fullscreen), and controls (Sensitivity, Inversion).
    /// Automatically initializes and applies options before any scene loads.
    /// </summary>
    public static class GameSettings
    {
        // Canonical persisted PlayerPrefs keys.
        public const string KeyMasterVol    = "Duskborn_MasterVol";
        public const string KeyMusicVol     = "Duskborn_MusicVol";
        public const string KeySfxVol       = "Duskborn_SfxVol";
        public const string KeyAmbientVol   = "Duskborn_AmbientVol";
        public const string KeyUiVol        = "Duskborn_UiVol";
        public const string KeyFullScreen   = "Duskborn_FullScreen";
        public const string KeySensitivity  = "Duskborn_MouseSensitivity";
        public const string KeyInvertPitch  = "Duskborn_InvertPitch";

        // Legacy key for backward compatibility.
        private const string LegacyKeyMasterVol = "Duskborn_MasterVolume";

        // Current in-memory values.
        public static float MasterVolume { get; private set; } = 1.0f;
        public static float MusicVolume { get; private set; } = 0.75f;
        public static float SfxVolume { get; private set; } = 0.90f;
        public static float AmbienceVolume { get; private set; } = 0.60f;
        public static float UiVolume { get; private set; } = 0.85f;
        public static bool FullScreen { get; private set; } = true;
        public static float MouseSensitivity { get; private set; } = 0.15f;
        public static bool InvertPitch { get; private set; } = false;

        private static bool _isInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            LoadAll();
            ApplyAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyAll();
        }

        /// <summary>
        /// Load all saved PlayerPrefs options or restore defaults.
        /// </summary>
        public static void LoadAll()
        {
            float legacyMaster = PlayerPrefs.GetFloat(LegacyKeyMasterVol, 1.0f);
            MasterVolume     = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVol, legacyMaster));
            MusicVolume      = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVol, 0.75f));
            SfxVolume        = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVol, 0.90f));
            AmbienceVolume   = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyAmbientVol, 0.60f));
            UiVolume         = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyUiVol, 0.85f));
            FullScreen       = PlayerPrefs.GetInt(KeyFullScreen, Screen.fullScreen ? 1 : 0) == 1;
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 0.15f), 0.02f, 0.60f);
            InvertPitch      = PlayerPrefs.GetInt(KeyInvertPitch, 0) == 1;
            _isInitialized   = true;
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyMasterVol, MasterVolume);
            PlayerPrefs.SetFloat(LegacyKeyMasterVol, MasterVolume);
            AudioListener.volume = MasterVolume;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMasterVolume(MasterVolume);
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyMusicVol, MusicVolume);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicVolume(MusicVolume);
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeySfxVol, SfxVolume);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetSfxVolume(SfxVolume);
        }

        public static void SetAmbienceVolume(float value)
        {
            AmbienceVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyAmbientVol, AmbienceVolume);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetAmbienceVolume(AmbienceVolume);
        }

        public static void SetUiVolume(float value)
        {
            UiVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyUiVol, UiVolume);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetUiVolume(UiVolume);
        }

        public static void SetFullScreen(bool value)
        {
            FullScreen = value;
            PlayerPrefs.SetInt(KeyFullScreen, FullScreen ? 1 : 0);
            Screen.fullScreen = FullScreen;
        }

        public static void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.02f, 0.60f);
            PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
            if (PlayerCameraController.LocalInstance != null)
                PlayerCameraController.LocalInstance.SetSensitivity(MouseSensitivity);
        }

        public static void SetInvertPitch(bool value)
        {
            InvertPitch = value;
            PlayerPrefs.SetInt(KeyInvertPitch, InvertPitch ? 1 : 0);
            if (PlayerCameraController.LocalInstance != null)
                PlayerCameraController.LocalInstance.InvertPitch = InvertPitch;
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Apply all loaded settings to the audio, display, and camera subsystems.
        /// </summary>
        public static void ApplyAll()
        {
            if (!_isInitialized) LoadAll();

            AudioListener.volume = MasterVolume;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(MasterVolume);
                AudioManager.Instance.SetMusicVolume(MusicVolume);
                AudioManager.Instance.SetSfxVolume(SfxVolume);
                AudioManager.Instance.SetAmbienceVolume(AmbienceVolume);
                AudioManager.Instance.SetUiVolume(UiVolume);
            }

            Screen.fullScreen = FullScreen;

            if (PlayerCameraController.LocalInstance != null)
            {
                PlayerCameraController.LocalInstance.SetSensitivity(MouseSensitivity);
                PlayerCameraController.LocalInstance.InvertPitch = InvertPitch;
            }
        }
    }
}
