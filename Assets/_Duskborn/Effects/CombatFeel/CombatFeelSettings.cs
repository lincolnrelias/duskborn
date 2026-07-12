using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Effects
{
    // Scene singleton — add to a GameObject and set toggles before entering Play.
    public class CombatFeelSettings : MonoBehaviour
    {
        public static CombatFeelSettings Instance { get; private set; }

        [Header("Hit Flash")]
        public bool  hitFlashEnabled  = true;
        public float hitFlashDuration = 0.08f;
        public Color hitFlashColor    = Color.white;

        [Header("Knockback & Flinch")]
        public bool  knockbackEnabled  = true;
        public float knockbackDistance = 0.6f;
        public float knockbackDuration = 0.12f;
        public float flinchDuration    = 0.25f;

        [Header("Camera Shake")]
        public bool  cameraShakeEnabled = true;
        public float shakeOnHitDealt    = 0.06f;
        public float shakeOnHitTaken    = 0.18f;
        public float shakeDuration      = 0.2f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DuskLog.Warn(LogChannel.Effects, "Duplicate CombatFeelSettings — destroying this one.");
                Destroy(this);
                return;
            }
            Instance = this;
            DuskLog.Log(LogChannel.Effects,
                $"CombatFeel: flash={hitFlashEnabled} knockback={knockbackEnabled} shake={cameraShakeEnabled}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
