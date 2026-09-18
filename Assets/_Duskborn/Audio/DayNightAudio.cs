using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Audio
{
    /// <summary>
    /// Gerencia a trilha sonora adaptativa e os efeitos sonoros de transição do Ciclo Dia/Noite.
    /// Executa o berrante do alvorecer e a música serena de exploração durante o dia,
    /// e o berrante de guerra e a percussão frenética de combate durante a noite.
    /// </summary>
    public class DayNightAudio : MonoBehaviour
    {
        [Header("Trilhas Musicais")]
        [SerializeField] private AudioClip dayMusic;
        [SerializeField] private AudioClip nightMusic;

        [Header("Stingers de Transição")]
        [SerializeField] private AudioClip dawnHorn;
        [SerializeField] private AudioClip nightHorn;

        [Header("Configurações")]
        [SerializeField] private float musicFadeTime = 2.5f;
        [SerializeField] [Range(0f, 1f)] private float hornVolume = 0.9f;

        private void Start()
        {
            LoadClipsIfNull();

            if (DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.OnDayStart += HandleDayStart;
                DayNightCycle.Instance.OnNightStart += HandleNightStart;

                // Inicia a trilha apropriada para a fase atual
                if (DayNightCycle.Instance.IsDay)
                    HandleDayStart();
                else
                    HandleNightStart(DayNightCycle.Instance.CurrentNight);
            }
            else
            {
                // Fallback caso não haja DayNightCycle na cena (ex: teste rápido)
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
