#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Audio.Editor
{
    /// <summary>
    /// Utilitário de reprodução de áudio diretamente no Unity Editor sem necessidade de entrar no Play Mode.
    /// Utiliza reflexão sobre UnityEditor.AudioUtil, compatível com Unity 6 e versões modernas.
    /// </summary>
    public static class AudioPreviewUtility
    {
        private static Type _audioUtilType;
        private static MethodInfo _playPreviewMethod;
        private static MethodInfo _stopAllPreviewMethod;
        private static MethodInfo _isPlayingMethod;
        private static bool _initialized;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var assembly = typeof(AudioImporter).Assembly;
                _audioUtilType = assembly.GetType("UnityEditor.AudioUtil");

                if (_audioUtilType != null)
                {
                    // Busca variações de assinatura de PlayPreviewClip / PlayClip
                    _playPreviewMethod = _audioUtilType.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                                      ?? _audioUtilType.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                                      ?? _audioUtilType.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public)
                                      ?? _audioUtilType.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public);

                    // Busca variações de StopAllPreviewClips / StopAllClips
                    _stopAllPreviewMethod = _audioUtilType.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)
                                         ?? _audioUtilType.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);

                    // Busca variações de IsPreviewClipPlaying / IsClipPlaying
                    _isPlayingMethod = _audioUtilType.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public)
                                    ?? _audioUtilType.GetMethod("IsClipPlaying", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip) }, null)
                                    ?? _audioUtilType.GetMethod("IsClipPlaying", BindingFlags.Static | BindingFlags.Public);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioPreviewUtility] Falha ao inicializar AudioUtil via reflexão: {ex.Message}");
            }
        }

        public static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
        {
            if (clip == null) return;
            Initialize();

            StopAll();

            if (_playPreviewMethod != null)
            {
                var parameters = _playPreviewMethod.GetParameters();
                if (parameters.Length == 3)
                {
                    _playPreviewMethod.Invoke(null, new object[] { clip, startSample, loop });
                }
                else if (parameters.Length == 1)
                {
                    _playPreviewMethod.Invoke(null, new object[] { clip });
                }
            }
            else
            {
                // Fallback via AudioSource temporário
                PlayClipViaTempSource(clip);
            }
        }

        public static void StopAll()
        {
            Initialize();

            if (_stopAllPreviewMethod != null)
            {
                _stopAllPreviewMethod.Invoke(null, null);
            }

            if (_tempAudioSource != null && _tempAudioSource.isPlaying)
            {
                _tempAudioSource.Stop();
            }
        }

        public static bool IsPlaying(AudioClip clip = null)
        {
            Initialize();

            if (_isPlayingMethod != null)
            {
                var parameters = _isPlayingMethod.GetParameters();
                if (parameters.Length == 1 && clip != null)
                {
                    return (bool)_isPlayingMethod.Invoke(null, new object[] { clip });
                }
                if (parameters.Length == 0)
                {
                    return (bool)_isPlayingMethod.Invoke(null, null);
                }
            }

            return _tempAudioSource != null && _tempAudioSource.isPlaying;
        }

        // Fallback AudioSource
        private static AudioSource _tempAudioSource;
        private static void PlayClipViaTempSource(AudioClip clip)
        {
            if (_tempAudioSource == null)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags("[AudioPreview_Temp]", HideFlags.HideAndDontSave, typeof(AudioSource));
                _tempAudioSource = go.GetComponent<AudioSource>();
            }

            _tempAudioSource.clip = clip;
            _tempAudioSource.spatialBlend = 0f;
            _tempAudioSource.volume = 1f;
            _tempAudioSource.Play();
        }
    }
}
#endif
