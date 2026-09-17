using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Audio
{
    /// <summary>
    /// Gerenciador central de áudio do Duskborn.
    /// Controla canais de volume (Master, Music, SFX, Ambience, UI), crossfade de trilhas musicais
    /// e reprodução 2D e 3D com variação orgânica de pitch e volume.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[AudioManager]");
                        _instance = go.AddComponent<AudioManager>();
                    }
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("[AudioManager]");
                _instance = go.AddComponent<AudioManager>();
            }
        }

        private const string PrefMasterVolume = "Duskborn_MasterVol";
        private const string PrefMusicVolume  = "Duskborn_MusicVol";
        private const string PrefSfxVolume    = "Duskborn_SfxVol";
        private const string PrefAmbientVol   = "Duskborn_AmbientVol";
        private const string PrefUiVolume     = "Duskborn_UiVol";

        [Header("Volumes Gerais")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float uiVolume = 0.85f;

        public float MasterVolume   => masterVolume;
        public float MusicVolume    => musicVolume;
        public float SfxVolume      => sfxVolume;
        public float AmbienceVolume => ambienceVolume;
        public float UiVolume       => uiVolume;

        // Fontes de áudio dedicadas
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private bool _activeSourceIsA = true;
        private Coroutine _musicCrossfadeRoutine;

        private AudioSource _ambienceSource;
        private AudioSource _uiSource;
        private AudioSource _sfx2DSource;

        // Pool de fontes 3D para evitar alocações constantes
        private readonly List<AudioSource> _pool3D = new();
        private const int InitialPoolSize = 8;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVolumePreferences();
            SetupInternalSources();
        }

        private void SetupInternalSources()
        {
            _musicSourceA = gameObject.AddComponent<AudioSource>();
            _musicSourceA.loop = true;
            _musicSourceA.playOnAwake = false;
            _musicSourceA.spatialBlend = 0f;

            _musicSourceB = gameObject.AddComponent<AudioSource>();
            _musicSourceB.loop = true;
            _musicSourceB.playOnAwake = false;
            _musicSourceB.spatialBlend = 0f;

            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.loop = true;
            _ambienceSource.playOnAwake = false;
            _ambienceSource.spatialBlend = 0f;

            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.loop = false;
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;

            _sfx2DSource = gameObject.AddComponent<AudioSource>();
            _sfx2DSource.loop = false;
            _sfx2DSource.playOnAwake = false;
            _sfx2DSource.spatialBlend = 0f;

            for (int i = 0; i < InitialPoolSize; i++)
            {
                CreatePoolSource();
            }
        }

        private AudioSource CreatePoolSource()
        {
            var child = new GameObject($"AudioSource3D_{_pool3D.Count}");
            child.transform.SetParent(transform);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2f;
            src.maxDistance = 40f;
            _pool3D.Add(src);
            return src;
        }

        private void LoadVolumePreferences()
        {
            masterVolume   = PlayerPrefs.GetFloat(PrefMasterVolume, masterVolume);
            musicVolume    = PlayerPrefs.GetFloat(PrefMusicVolume, musicVolume);
            sfxVolume      = PlayerPrefs.GetFloat(PrefSfxVolume, sfxVolume);
            ambienceVolume = PlayerPrefs.GetFloat(PrefAmbientVol, ambienceVolume);
            uiVolume       = PlayerPrefs.GetFloat(PrefUiVolume, uiVolume);
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefMasterVolume, masterVolume);
            UpdateAllVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefMusicVolume, musicVolume);
            UpdateAllVolumes();
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefSfxVolume, sfxVolume);
            UpdateAllVolumes();
        }

        public void SetAmbienceVolume(float volume)
        {
            ambienceVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefAmbientVol, ambienceVolume);
            UpdateAllVolumes();
        }

        public void SetUiVolume(float volume)
        {
            uiVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefUiVolume, uiVolume);
            UpdateAllVolumes();
        }

        private void UpdateAllVolumes()
        {
            float activeMusicVol = masterVolume * musicVolume;
            if (_activeSourceIsA)
            {
                _musicSourceA.volume = activeMusicVol;
            }
            else
            {
                _musicSourceB.volume = activeMusicVol;
            }

            if (_ambienceSource != null)
                _ambienceSource.volume = masterVolume * ambienceVolume;
        }

        // ── Trilha Sonora e Crossfade ──────────────────────────────────────────

        public void PlayMusic(AudioClip clip, float fadeDuration = 1.5f, bool loop = true)
        {
            if (clip == null) return;

            var activeSource = _activeSourceIsA ? _musicSourceA : _musicSourceB;
            if (activeSource.clip == clip && activeSource.isPlaying) return;

            if (_musicCrossfadeRoutine != null)
                StopCoroutine(_musicCrossfadeRoutine);

            _musicCrossfadeRoutine = StartCoroutine(CrossfadeMusicRoutine(clip, fadeDuration, loop));
        }

        public void StopMusic(float fadeDuration = 1.0f)
        {
            if (_musicCrossfadeRoutine != null)
                StopCoroutine(_musicCrossfadeRoutine);

            _musicCrossfadeRoutine = StartCoroutine(FadeOutMusicRoutine(fadeDuration));
        }

        private IEnumerator CrossfadeMusicRoutine(AudioClip newClip, float duration, bool loop)
        {
            var oldSource = _activeSourceIsA ? _musicSourceA : _musicSourceB;
            var newSource = _activeSourceIsA ? _musicSourceB : _musicSourceA;
            _activeSourceIsA = !_activeSourceIsA;

            newSource.clip = newClip;
            newSource.loop = loop;
            newSource.volume = 0f;
            newSource.Play();

            float elapsed = 0f;
            float targetVol = masterVolume * musicVolume;
            float startOldVol = oldSource.isPlaying ? oldSource.volume : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                newSource.volume = Mathf.Lerp(0f, targetVol, t);
                if (oldSource.isPlaying)
                    oldSource.volume = Mathf.Lerp(startOldVol, 0f, t);

                yield return null;
            }

            newSource.volume = targetVol;
            if (oldSource.isPlaying)
            {
                oldSource.Stop();
                oldSource.volume = 0f;
            }
            _musicCrossfadeRoutine = null;
        }

        private IEnumerator FadeOutMusicRoutine(float duration)
        {
            var activeSource = _activeSourceIsA ? _musicSourceA : _musicSourceB;
            float startVol = activeSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                activeSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }

            activeSource.Stop();
            activeSource.volume = 0f;
            _musicCrossfadeRoutine = null;
        }

        // ── Efeitos Sonoros 2D e UI ───────────────────────────────────────────

        public void PlaySfx(AudioClip clip, float volumeScale = 1.0f, float pitchJitter = 0.04f)
        {
            if (clip == null) return;
            float pitch = 1.0f + Random.Range(-pitchJitter, pitchJitter);
            _sfx2DSource.pitch = pitch;
            _sfx2DSource.PlayOneShot(clip, volumeScale * masterVolume * sfxVolume);
        }

        public void PlayUISfx(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null) return;
            _uiSource.pitch = 1.0f;
            _uiSource.PlayOneShot(clip, volumeScale * masterVolume * uiVolume);
        }

        // ── Efeitos Sonoros 3D com Posicionamento ──────────────────────────────

        public AudioSource PlayAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1.0f,
                                      float minDistance = 2f, float maxDistance = 40f, float pitchJitter = 0.05f)
        {
            if (clip == null) return null;

            AudioSource src = null;
            for (int i = 0; i < _pool3D.Count; i++)
            {
                if (!_pool3D[i].isPlaying)
                {
                    src = _pool3D[i];
                    break;
                }
            }

            if (src == null)
                src = CreatePoolSource();

            src.transform.position = position;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.pitch = 1.0f + Random.Range(-pitchJitter, pitchJitter);
            src.volume = volumeScale * masterVolume * sfxVolume;
            src.clip = clip;
            src.Play();

            return src;
        }
    }
}
