using System;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Finite midnight hold: defeat resumes the remaining clock instead of ending the night.</summary>
    public static class HollowWardenNightRules
    {
        // SampleScene currently assigns only its first normal wave definition.
        // Reuse that configured budget for night three when no explicit entry exists.
        public static int DefinitionIndex(int night, int count) => count <= 0 || night <= 0 ? -1
            : night <= count ? night - 1 : night == 3 ? count - 1 : -1;

        public static float AdvanceHeldClock(float remaining, float duration, float delta) =>
            Math.Max(duration * .45f, remaining - Math.Max(0, delta));

        public static bool RepeatWaves(int night, bool isNight, float elapsed, float duration) =>
            night == 3 && isNight && elapsed >= Math.Max(1, duration);
    }
}
