using Duskborn;
using Duskborn.Gameplay.Equipment;
using UnityEngine;

namespace Duskborn.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class WeaponAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float pitchVariation = 0.06f;
        [SerializeField] [Range(0f, 1f)] private float swingVolume = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float hitVolume = 1.0f;

        private WeaponActionPlayer _actionPlayer;
        private WeaponHitNotifier  _notifier;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            _actionPlayer = GetComponent<WeaponActionPlayer>();
            _notifier     = GetComponent<WeaponHitNotifier>();

            if (audioSource == null) DuskLog.Error(LogChannel.Audio, $"[{name}] no AudioSource found.");
            else
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D espacial para multiplayer
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 35f;
            }

            if (_actionPlayer == null) DuskLog.Warn (LogChannel.Audio, $"[{name}] no WeaponActionPlayer on same GameObject — profile lookups will fail.");
            if (_notifier     == null) DuskLog.Warn (LogChannel.Audio, $"[{name}] no WeaponHitNotifier on same GameObject — hit audio will not play.");
            else                     { _notifier.OnHit += HandleHit; DuskLog.Log(LogChannel.Audio, $"[{name}] subscribed to WeaponHitNotifier."); }
        }

        private void OnDestroy()
        {
            if (_notifier != null) _notifier.OnHit -= HandleHit;
        }

        public void PlaySwing(WeaponAudioProfile profile)
        {
            if (profile == null) { DuskLog.Warn(LogChannel.Audio, $"[{name}] PlaySwing: no profile on current weapon."); return; }
            var clip = profile.PickSwing();
            if (clip == null) { DuskLog.Warn(LogChannel.Audio, $"[{name}] PlaySwing: '{profile.name}' has no swing clips."); return; }
            DuskLog.Log(LogChannel.Audio, $"[{name}] PlaySwing: '{clip.name}'.");

            PlayWithPitch(clip, swingVolume);
        }

        private void HandleHit(WeaponHitNotifier.HitData data)
        {
            DuskLog.Log(LogChannel.Audio, $"[{name}] HandleHit: tag='{data.Tag}' override={data.AudioOverride?.name ?? "none"}.");

            // Priority: explicit override in HitData → active skill's override → profile tag lookup
            var clip = data.AudioOverride
                    ?? _actionPlayer?.CurrentSkill?.hitAudioOverride
                    ?? _actionPlayer?.CurrentWeapon?.AudioProfile?.PickHit(data.Tag);

            if (clip != null)
            {
                DuskLog.Log(LogChannel.Audio, $"[{name}] playing '{clip.name}'.");
                PlayWithPitch(clip, hitVolume);
            }
            else
            {
                DuskLog.Warn(LogChannel.Audio,
                    $"[{name}] no hit clip for tag '{data.Tag}' — add a surface entry or a Default fallback to the profile.");
            }
        }

        private void PlayWithPitch(AudioClip clip, float volume)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            float busVol = AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f;
            audioSource.PlayOneShot(clip, volume * busVol);
        }
    }
}
