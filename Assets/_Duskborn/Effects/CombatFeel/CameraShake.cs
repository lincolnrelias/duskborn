using UnityEngine;

namespace Duskborn.Effects
{
    // Decaying positional camera shake. PlayerController adds Offset after its camera follow.
    public static class CameraShake
    {
        private static float _magnitude;
        private static float _endTime;
        private static float _duration;

        public static void ShakeDealt()
        {
            var s = CombatFeelSettings.Instance;
            if (s == null || !s.cameraShakeEnabled) return;
            Shake(s.shakeOnHitDealt, s.shakeDuration);
        }

        public static void ShakeTaken()
        {
            var s = CombatFeelSettings.Instance;
            if (s == null || !s.cameraShakeEnabled) return;
            Shake(s.shakeOnHitTaken, s.shakeDuration);
        }

        private static void Shake(float magnitude, float duration)
        {
            if (magnitude <= 0f || duration <= 0f) return;
            // A stronger shake overrides a weaker in-flight one; equal restarts the timer.
            if (Time.unscaledTime < _endTime && magnitude < _magnitude) return;
            _magnitude = magnitude;
            _duration  = duration;
            _endTime   = Time.unscaledTime + duration;
        }

        public static Vector3 Offset
        {
            get
            {
                float remaining = _endTime - Time.unscaledTime;
                if (remaining <= 0f) return Vector3.zero;
                float damper = remaining / _duration;
                return new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)) * (_magnitude * damper);
            }
        }
    }
}
