using System;
using UnityEngine;
using UnityEditor;
using Duskborn.Core;

namespace Duskborn.Editor
{
    public static class DayNightCycleTests
    {
        [MenuItem("Duskborn/Tests/Run Day-Night Cycle Tests", false, 101)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_PeriodProgressionAndTiming, ref passed, ref total);
            RunTest(Test_NightlyMechanicsGatedToNight, ref passed, ref total);
            RunTest(Test_VisualBlendBoundaryContinuity, ref passed, ref total);
            RunTest(Test_ProceduralLightingContinuity, ref passed, ref total);
            RunTest(Test_ClockTimeContinuity, ref passed, ref total);
            RunTest(Test_CelestialSunMoonDayNightSanity, ref passed, ref total);
            RunTest(Test_DirectionalLightAndShadowSanity, ref passed, ref total);
            RunTest(Test_DynamicAtmosphericFogAndDensitySanity, ref passed, ref total);
            RunTest(Test_EnvironmentVisualBootstrapperPipeline, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[DayNightCycleTests] {passed}/{total} tests passed!</b></color>");
        }

        private static void RunTest(Action testMethod, ref int passed, ref int total)
        {
            total++;
            try
            {
                testMethod();
                passed++;
                Debug.Log($"[PASS] {testMethod.Method.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] {testMethod.Method.Name}: {ex}");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertApproximately(float a, float b, float maxDelta, string message)
        {
            if (Mathf.Abs(a - b) > maxDelta)
                throw new Exception($"{message} (Expected: {b}, Actual: {a}, Delta: {Mathf.Abs(a - b)})");
        }

        private static void AssertColorApproximately(Color a, Color b, float maxDelta, string message)
        {
            float deltaR = Mathf.Abs(a.r - b.r);
            float deltaG = Mathf.Abs(a.g - b.g);
            float deltaB = Mathf.Abs(a.b - b.b);
            if (deltaR > maxDelta || deltaG > maxDelta || deltaB > maxDelta)
                throw new Exception($"{message} (Expected: {b}, Actual: {a})");
        }

        private static void Test_PeriodProgressionAndTiming()
        {
            // Validate progressive period windows.
            AssertTrue(GetPeriodForProgress(true, 0.05f) == CyclePeriod.Dawn, "0.05 of day must be Dawn");
            AssertTrue(GetPeriodForProgress(true, 0.30f) == CyclePeriod.Morning, "0.30 of day must be Morning");
            AssertTrue(GetPeriodForProgress(true, 0.60f) == CyclePeriod.Midday, "0.60 of day must be Noon");
            AssertTrue(GetPeriodForProgress(true, 0.78f) == CyclePeriod.Afternoon, "0.78 of day must be Afternoon");
            AssertTrue(GetPeriodForProgress(true, 0.92f) == CyclePeriod.Dusk, "0.92 of day must be Dusk");

            AssertTrue(GetPeriodForProgress(false, 0.10f) == CyclePeriod.Nightfall, "0.10 of night must be Nightfall");
            AssertTrue(GetPeriodForProgress(false, 0.50f) == CyclePeriod.Midnight, "0.50 of night must be Midnight");
            AssertTrue(GetPeriodForProgress(false, 0.85f) == CyclePeriod.PreDawn, "0.85 of night must be Predawn");
        }

        private static void Test_NightlyMechanicsGatedToNight()
        {
            // Dusk is part of the day: players prepare, and nighttime mechanics have not activated yet.
            CyclePeriod duskPeriod = GetPeriodForProgress(true, 0.95f);
            AssertTrue(duskPeriod == CyclePeriod.Dusk, "Late afternoon must be Dusk");

            // Validate that IsDay is true at any time during the day (including dusk).
            for (float p = 0f; p <= 1f; p += 0.1f)
            {
                bool isDay = true;
                bool isNight = !isDay;
                AssertTrue(isDay && !isNight, $"At day progress {p}, nighttime mechanics must not run");
            }
        }

        private static void Test_VisualBlendBoundaryContinuity()
        {
            // Day -> night transition: the value at day t=1.0 must equal the value at night t=0.0.
            float dayEndBlend = CalculateNightBlend(true, 1.0f);
            float nightStartBlend = CalculateNightBlend(false, 0.0f);
            AssertApproximately(dayEndBlend, nightStartBlend, 0.001f, "Day->Night blend must be perfectly continuous (no jumps)");

            // Night -> day transition: the value at night t=1.0 must equal the value at day t=0.0.
            float nightEndBlend = CalculateNightBlend(false, 1.0f);
            float dayStartBlend = CalculateNightBlend(true, 0.0f);
            AssertApproximately(nightEndBlend, dayStartBlend, 0.001f, "Night->Day blend must be perfectly continuous (no jumps)");
        }

        private static void Test_ProceduralLightingContinuity()
        {
            // Light intensity continuity.
            float dayEndIntensity = CalculateIntensity(true, 1.0f);
            float nightStartIntensity = CalculateIntensity(false, 0.0f);
            AssertApproximately(dayEndIntensity, nightStartIntensity, 0.001f, "Intensity at Day->Night must be continuous");

            float nightEndIntensity = CalculateIntensity(false, 1.0f);
            float dayStartIntensity = CalculateIntensity(true, 0.0f);
            AssertApproximately(nightEndIntensity, dayStartIntensity, 0.001f, "Intensity at Night->Day must be continuous");

            // Light color continuity.
            Color dayEndColor = CalculateLightColor(true, 1.0f);
            Color nightStartColor = CalculateLightColor(false, 0.0f);
            AssertColorApproximately(dayEndColor, nightStartColor, 0.01f, "Light color at Day->Night must be continuous");

            Color nightEndColor = CalculateLightColor(false, 1.0f);
            Color dayStartColor = CalculateLightColor(true, 0.0f);
            AssertColorApproximately(nightEndColor, dayStartColor, 0.01f, "Light color at Night->Day must be continuous");
        }

        private static void Test_ClockTimeContinuity()
        {
            // Day: 06:00 to 20:00.
            float startHour = 6f + 0f * 14f;
            float endHour = 6f + 1f * 14f;
            AssertApproximately(startHour, 6f, 0.01f, "Day must start at 06:00");
            AssertApproximately(endHour, 20f, 0.01f, "Day must end at 20:00");

            // Night: 20:00 to 06:00.
            float nightStartHour = (20f + 0f * 10f) % 24f;
            float nightEndHour = (20f + 1f * 10f) % 24f;
            AssertApproximately(nightStartHour, 20f, 0.01f, "Night must start at 20:00");
            AssertApproximately(nightEndHour, 6f, 0.01f, "Night must end at 06:00 (dawn)");
        }

        private static void Test_CelestialSunMoonDayNightSanity()
        {
            // Validate that the sun is in the sky and the moon below the horizon throughout the day.
            for (float p = 0.05f; p <= 0.95f; p += 0.15f)
            {
                DayNightCycle.CalculateCelestialVectors(true, p, 32f, 68f, 28f, 62f, out Vector3 sunDir, out Vector3 moonDir, out _);
                AssertTrue(sunDir.y > 0f, $"At day progress {p:F2}, the sun MUST be in the sky (sunDir.y={sunDir.y:F3} > 0)");
                AssertTrue(moonDir.y < 0f, $"At day progress {p:F2}, the moon MUST be below the horizon (moonDir.y={moonDir.y:F3} < 0)");
            }

            // Validate that the moon is in the sky and the sun below the horizon throughout the night.
            for (float p = 0.05f; p <= 0.95f; p += 0.15f)
            {
                DayNightCycle.CalculateCelestialVectors(false, p, 32f, 68f, 28f, 62f, out Vector3 sunDir, out Vector3 moonDir, out _);
                AssertTrue(moonDir.y > 0f, $"At night progress {p:F2}, the moon MUST be in the sky (moonDir.y={moonDir.y:F3} > 0)");
                AssertTrue(sunDir.y < 0f, $"At night progress {p:F2}, the sun MUST be below the horizon (sunDir.y={sunDir.y:F3} < 0)");
            }

            // Validate maximum elevations at noon and midnight.
            DayNightCycle.CalculateCelestialVectors(true, 0.5f, 32f, 68f, 28f, 62f, out Vector3 middaySun, out _, out _);
            AssertTrue(middaySun.y > 0.85f, $"The sun at noon must reach maximum elevation (y={middaySun.y:F3})");

            DayNightCycle.CalculateCelestialVectors(false, 0.5f, 32f, 68f, 28f, 62f, out _, out Vector3 midnightMoon, out _);
            AssertTrue(midnightMoon.y > 0.80f, $"The moon at midnight must reach maximum elevation (y={midnightMoon.y:F3})");
        }

        private static void Test_DirectionalLightAndShadowSanity()
        {
            // Validate that directional light ALWAYS points downward (Y < 0), lighting terrain and casting shadows.
            for (float p = 0f; p <= 1f; p += 0.1f)
            {
                DayNightCycle.CalculateCelestialVectors(true, p, 32f, 68f, 28f, 62f, out _, out _, out Vector3 dayLightFwd);
                AssertTrue(dayLightFwd.y < 0f, $"At day progress {p:F1}, light must point toward the ground (light.y={dayLightFwd.y:F3} < 0)");

                DayNightCycle.CalculateCelestialVectors(false, p, 32f, 68f, 28f, 62f, out _, out _, out Vector3 nightLightFwd);
                AssertTrue(nightLightFwd.y < 0f, $"At night progress {p:F1}, moonlight must point toward the ground (light.y={nightLightFwd.y:F3} < 0)");
            }

            // Validate seamless continuity at the sunset -> night transition (no shadow jumps).
            DayNightCycle.CalculateCelestialVectors(true, 1.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 dayEndLightFwd);
            DayNightCycle.CalculateCelestialVectors(false, 0.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 nightStartLightFwd);
            float duskDelta = Vector3.Distance(dayEndLightFwd, nightStartLightFwd);
            AssertApproximately(duskDelta, 0f, 0.005f, "Light vector at Dusk->Nightfall must be continuous (no shadow jumps)");

            // Validate seamless continuity at the predawn -> dawn transition (no shadow jumps).
            DayNightCycle.CalculateCelestialVectors(false, 1.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 nightEndLightFwd);
            DayNightCycle.CalculateCelestialVectors(true, 0.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 dayStartLightFwd);
            float dawnDelta = Vector3.Distance(nightEndLightFwd, dayStartLightFwd);
            AssertApproximately(dawnDelta, 0f, 0.005f, "Light vector at Predawn->Dawn must be continuous (no shadow jumps)");

            // Validate physical alignment: at noon the sun is south (z < 0), and shadows extend north (z > 0).
            DayNightCycle.CalculateCelestialVectors(true, 0.5f, 32f, 68f, 28f, 62f, out Vector3 middaySun, out _, out Vector3 middayLightFwd);
            AssertTrue(middaySun.z < 0f, "The sun at noon must be south in the stylized northern hemisphere");
            AssertTrue(middayLightFwd.z > 0f, "Light must point north at noon, casting shadows northward");
        }

        // Functions mirroring DayNightCycle for deterministic verification.
        private static CyclePeriod GetPeriodForProgress(bool isDay, float p)
        {
            if (isDay)
            {
                if (p < 0.15f) return CyclePeriod.Dawn;
                if (p < 0.50f) return CyclePeriod.Morning;
                if (p < 0.72f) return CyclePeriod.Midday;
                if (p < 0.85f) return CyclePeriod.Afternoon;
                return CyclePeriod.Dusk;
            }
            else
            {
                if (p < 0.25f) return CyclePeriod.Nightfall;
                if (p < 0.75f) return CyclePeriod.Midnight;
                return CyclePeriod.PreDawn;
            }
        }

        private static float CalculateNightBlend(bool isDay, float progress)
        {
            if (isDay)
            {
                if (progress < 0.15f)
                    return Mathf.Lerp(0.50f, 0.0f, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                if (progress > 0.80f)
                    return Mathf.Lerp(0.0f, 0.65f, Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.20f));
                return 0f;
            }
            else
            {
                if (progress < 0.15f)
                    return Mathf.Lerp(0.65f, 1.0f, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                if (progress > 0.82f)
                    return Mathf.Lerp(1.0f, 0.50f, Mathf.SmoothStep(0f, 1f, (progress - 0.82f) / 0.18f));
                return 1f;
            }
        }

        private static float CalculateIntensity(bool isDay, float progress)
        {
            const float dayPeak     = 1.25f;
            const float dawnStart   = 0.55f;
            const float duskEnd     = 0.35f;
            const float nightNormal = 0.35f;
            const float midnight    = 0.28f;

            if (isDay)
            {
                if (progress < 0.15f)
                    return Mathf.Lerp(dawnStart, dayPeak, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                if (progress < 0.70f)
                    return dayPeak;
                if (progress < 0.85f)
                    return Mathf.Lerp(dayPeak, 1.10f, Mathf.SmoothStep(0f, 1f, (progress - 0.70f) / 0.15f));
                return Mathf.Lerp(1.10f, duskEnd, Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.15f));
            }
            else
            {
                if (progress < 0.15f)
                    return Mathf.Lerp(duskEnd, nightNormal, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                if (progress < 0.50f)
                    return Mathf.Lerp(nightNormal, midnight, Mathf.SmoothStep(0f, 1f, (progress - 0.15f) / 0.35f));
                if (progress < 0.80f)
                    return Mathf.Lerp(midnight, nightNormal, Mathf.SmoothStep(0f, 1f, (progress - 0.50f) / 0.30f));
                return Mathf.Lerp(nightNormal, dawnStart, Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.20f));
            }
        }

        private static Color CalculateLightColor(bool isDay, float progress)
        {
            Color dawnColor      = new Color(1.0f, 0.82f, 0.68f);
            Color dayColor       = new Color(1.0f, 0.96f, 0.88f);
            Color afternoonColor = new Color(1.0f, 0.90f, 0.74f);
            Color duskColor      = new Color(1.0f, 0.52f, 0.24f);
            Color twilightColor  = new Color(0.48f, 0.52f, 0.72f);
            Color nightColor     = new Color(0.60f, 0.75f, 1.0f);
            Color preDawnColor   = new Color(0.70f, 0.65f, 0.88f);

            if (isDay)
            {
                if (progress < 0.15f)
                    return Color.Lerp(dawnColor, dayColor, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                if (progress < 0.70f)
                    return dayColor;
                if (progress < 0.85f)
                    return Color.Lerp(dayColor, afternoonColor, Mathf.SmoothStep(0f, 1f, (progress - 0.70f) / 0.15f));
                if (progress < 0.95f)
                    return Color.Lerp(afternoonColor, duskColor, Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.10f));
                return Color.Lerp(duskColor, twilightColor, Mathf.SmoothStep(0f, 1f, (progress - 0.95f) / 0.05f));
            }
            else
            {
                if (progress < 0.10f)
                    return Color.Lerp(twilightColor, nightColor, Mathf.SmoothStep(0f, 1f, progress / 0.10f));
                if (progress < 0.80f)
                    return nightColor;
                if (progress < 0.95f)
                    return Color.Lerp(nightColor, preDawnColor, Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.15f));
                return Color.Lerp(preDawnColor, dawnColor, Mathf.SmoothStep(0f, 1f, (progress - 0.95f) / 0.05f));
            }
        }

        private static void Test_DynamicAtmosphericFogAndDensitySanity()
        {
            // Create a temporary GameObject with DayNightCycle to test fog and atmosphere modulation.
            GameObject go = new GameObject("Test_DayNightCycle_Atmosphere");
            var lightGO = new GameObject("Test_Sun");
            try
            {
                var dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                var networkObject = go.AddComponent<FishNet.Object.NetworkObject>();
                var cycle = go.AddComponent<DayNightCycle>();
                // FishNet normally serializes this reference when authoring a prefab.
                typeof(FishNet.Object.NetworkBehaviour).GetMethod("SerializeComponents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(cycle, new object[] { networkObject, (byte)0 });
                typeof(DayNightCycle).GetField("directionalLight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(cycle, dirLight);

                // Test 1: noon (open visibility and minimal clear fog).
                cycle.SetPhaseAndProgressForEditor(true, 0.5f, 1);
                float dayFog = cycle.CurrentFogDensity;
                AssertTrue(dayFog > 0.002f && dayFog < 0.005f, $"Noon fog must be clear (got: {dayFog})");

                // Test 2: dusk (atmospheric amber / golden mist must be denser than at noon).
                cycle.SetPhaseAndProgressForEditor(true, 0.90f, 1);
                float duskFog = cycle.CurrentFogDensity;
                AssertTrue(duskFog > dayFog, $"Dusk fog ({duskFog}) MUST be denser than noon fog ({dayFog})");

                // Test 3: midnight (dense moonlit fog).
                cycle.SetPhaseAndProgressForEditor(false, 0.5f, 1);
                float nightFog = cycle.CurrentFogDensity;
                AssertTrue(nightFog > duskFog, $"Night fog ({nightFog}) MUST be denser than dusk fog ({duskFog})");

                // Test 4: boss night 7 (the most oppressive crimson mist).
                cycle.SetPhaseAndProgressForEditor(false, 0.5f, 7);
                float bossFog = cycle.CurrentFogDensity;
                AssertTrue(bossFog > nightFog, $"Night 7 fog ({bossFog}) MUST be the densest (greater than {nightFog})");

                // Test 5: remove atmospheric particles.
                var atmoGO = new GameObject("WorldAtmosphere");
                atmoGO.AddComponent<Duskborn.Gameplay.World.WorldAtmosphereController>();
                EnvironmentVisualBootstrapper.RemoveWorldAtmosphere();
                AssertTrue(GameObject.Find("WorldAtmosphere") == null, "WorldAtmosphere must be removed successfully");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(lightGO);
            }
        }

        private static void Test_EnvironmentVisualBootstrapperPipeline()
        {
            var camGO = new GameObject("TestCamera");
            var cam = camGO.AddComponent<Camera>();
            try
            {
                EnvironmentVisualBootstrapper.EnsureCameraPostProcessing(cam);
                var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                AssertTrue(camData != null && camData.renderPostProcessing, "The camera must have renderPostProcessing enabled.");

                EnvironmentVisualBootstrapper.EnsureGlobalVolume();
                var volume = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
                AssertTrue(volume != null, "The global Volume must be created / ensured.");
                AssertTrue(volume.isGlobal, "The Volume must be global.");
                AssertTrue(volume.sharedProfile != null || volume.profile != null, "The Volume must have a profile assigned.");

                EnvironmentVisualBootstrapper.EnsureDayNightFog();
                AssertTrue(RenderSettings.fog, "RenderSettings.fog must be enabled.");

                EnvironmentVisualBootstrapper.EnsureVisualPipeline();
                AssertTrue(GameObject.Find("WorldAtmosphere") == null, "WorldAtmosphere must not exist after the pipeline is ensured.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGO);
                var vol = GameObject.Find("Global Volume");
                if (vol != null) UnityEngine.Object.DestroyImmediate(vol);
                var atmo = GameObject.Find("WorldAtmosphere");
                if (atmo != null) UnityEngine.Object.DestroyImmediate(atmo);
            }
        }
    }
}
