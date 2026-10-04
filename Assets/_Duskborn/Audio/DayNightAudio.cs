using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Audio
{
    /// <summary>
    /// Manages the adaptive soundtrack and transition sound effects for the day/night cycle.
    /// Plays the dawn horn and serene exploration music during the day,
    /// and the war horn and frantic combat percussion at night.
    /// </summary>
    public class DayNightAudio : MonoBehaviour
    {
        [Header("Music Tracks")]
        [SerializeField] private AudioClip dayMusic;
        [SerializeField] private AudioClip nightMusic;

        [Header("Transition Stingers")]
        [SerializeField] private AudioClip dawnHorn;
        [SerializeField] private AudioClip nightHorn;

        [Header("Settings")]
        [SerializeField] private float musicFadeTime = 2.5f;
        [SerializeField] [Range(0f, 1f)] private float hornVolume = 0.9f;

        private void Start()
        {
            LoadClipsIfNull();

            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnDayStart += HandleDayStart;
                DayNightCycle.Instance.OnNightStart += HandleNightStart;

                // Catch up the music without replaying a phase-transition horn.
                // OnDayStart fires once loading/reveal finishes; that event owns
                // the arrival horn, including when this component starts earlier.
                var currentMusic = DayNightCycle.Instance.IsDay ? dayMusic : nightMusic;
                if (currentMusic != null && AudioManager.Instance != null)
                    AudioManager.Instance.PlayMusic(currentMusic, musicFadeTime);
            }
            else
            {
                // Fallback when no DayNightCycle exists in the scene (e.g. a quick test).
                if (dayMusic != null && AudioManager.Instance != null)
                    AudioManager.Instance.PlayMusic(dayMusic, musicFadeTime);
            }
        }

        private void LoadClipsIfNull()
        {
            var db = AudioDatabase.Instance?.Music;
            if (db != null)
            {
                if (dayMusic == null)   dayMusic   = db.dayMusic;
                if (nightMusic == null) nightMusic = db.nightMusic;
                if (dawnHorn == null)   dawnHorn   = db.dawnHorn;
                if (nightHorn == null)  nightHorn  = db.nightHorn;

                musicFadeTime = db.musicFadeDuration;
                hornVolume = db.hornVolume;
            }
            else
            {
                if (dayMusic == null)   dayMusic   = Resources.Load<AudioClip>("Music/music_day_exploration");
                if (nightMusic == null) nightMusic = Resources.Load<AudioClip>("Music/music_night_combat");
                if (dawnHorn == null)   dawnHorn   = Resources.Load<AudioClip>("SFX/dawn_horn");
                if (nightHorn == null)  nightHorn  = Resources.Load<AudioClip>("SFX/night_horn");
            }
        }

        private void OnDestroy()
        {
            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnDayStart -= HandleDayStart;
                DayNightCycle.Instance.OnNightStart -= HandleNightStart;
            }
        }

        private void HandleDayStart()
        {
            if (dawnHorn != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(dawnHorn, hornVolume, pitchJitter: 0.02f);
            }

            if (dayMusic != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(dayMusic, musicFadeTime);
            }
        }

        private void HandleNightStart(int nightNumber)
        {
            if (nightHorn != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(nightHorn, hornVolume, pitchJitter: 0.02f);
            }

            if (nightMusic != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(nightMusic, musicFadeTime);
            }
        }
    }
}
