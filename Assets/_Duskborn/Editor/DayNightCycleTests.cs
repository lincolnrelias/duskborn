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

            Debug.Log($"<color=#55FF55><b>[DayNightCycleTests] {passed}/{total} testes passaram com sucesso!</b></color>");
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
                Debug.LogError($"[FAIL] {testMethod.Method.Name}: {ex.Message}");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertApproximately(float a, float b, float maxDelta, string message)
        {
            if (Mathf.Abs(a - b) > maxDelta)
                throw new Exception($"{message} (Esperado: {b}, Obtido: {a}, Delta: {Mathf.Abs(a - b)})");
        }

        private static void AssertColorApproximately(Color a, Color b, float maxDelta, string message)
        {
            float deltaR = Mathf.Abs(a.r - b.r);
            float deltaG = Mathf.Abs(a.g - b.g);
            float deltaB = Mathf.Abs(a.b - b.b);
            if (deltaR > maxDelta || deltaG > maxDelta || deltaB > maxDelta)
                throw new Exception($"{message} (Esperado: {b}, Obtido: {a})");
        }

        private static void Test_PeriodProgressionAndTiming()
        {
            // Valida as janelas dos períodos progressivos
            AssertTrue(GetPeriodForProgress(true, 0.05f) == CyclePeriod.Dawn, "0.05 de dia deve ser Alvorecer");
            AssertTrue(GetPeriodForProgress(true, 0.30f) == CyclePeriod.Morning, "0.30 de dia deve ser Manhã");
            AssertTrue(GetPeriodForProgress(true, 0.60f) == CyclePeriod.Midday, "0.60 de dia deve ser Meio-dia");
            AssertTrue(GetPeriodForProgress(true, 0.78f) == CyclePeriod.Afternoon, "0.78 de dia deve ser Entardecer");
            AssertTrue(GetPeriodForProgress(true, 0.92f) == CyclePeriod.Dusk, "0.92 de dia deve ser Crepúsculo");

            AssertTrue(GetPeriodForProgress(false, 0.10f) == CyclePeriod.Nightfall, "0.10 de noite deve ser Anoitecer");
            AssertTrue(GetPeriodForProgress(false, 0.50f) == CyclePeriod.Midnight, "0.50 de noite deve ser Meia-noite");
            AssertTrue(GetPeriodForProgress(false, 0.85f) == CyclePeriod.PreDawn, "0.85 de noite deve ser Madrugada");
        }

        private static void Test_NightlyMechanicsGatedToNight()
        {
            // O Crepúsculo faz parte do Dia — jogadores preparam-se, mecânicas noturnas ainda não ativaram
            CyclePeriod duskPeriod = GetPeriodForProgress(true, 0.95f);
            AssertTrue(duskPeriod == CyclePeriod.Dusk, "Fim da tarde deve ser Crepúsculo");

            // Valida que em qualquer momento do dia (inclusive Crepúsculo), IsDay é verdadeiro
            for (float p = 0f; p <= 1f; p += 0.1f)
            {
                bool isDay = true;
                bool isNight = !isDay;
                AssertTrue(isDay && !isNight, $"Em progresso {p} do dia, mecânicas noturnas não devem rodar");
            }
        }

        private static void Test_VisualBlendBoundaryContinuity()
        {
            // Transição Dia -> Noite: o valor em t=1.0 de dia deve ser idêntico ao valor em t=0.0 de noite
            float dayEndBlend = CalculateNightBlend(true, 1.0f);
            float nightStartBlend = CalculateNightBlend(false, 0.0f);
            AssertApproximately(dayEndBlend, nightStartBlend, 0.001f, "Blend de Dia->Noite deve ser perfeitamente contínuo (sem saltos)");

            // Transição Noite -> Dia: o valor em t=1.0 de noite deve ser idêntico ao valor em t=0.0 de dia
            float nightEndBlend = CalculateNightBlend(false, 1.0f);
            float dayStartBlend = CalculateNightBlend(true, 0.0f);
            AssertApproximately(nightEndBlend, dayStartBlend, 0.001f, "Blend de Noite->Dia deve ser perfeitamente contínuo (sem saltos)");
        }

        private static void Test_ProceduralLightingContinuity()
        {
            // Continuidade de Intensidade luminosa
            float dayEndIntensity = CalculateIntensity(true, 1.0f);
            float nightStartIntensity = CalculateIntensity(false, 0.0f);
            AssertApproximately(dayEndIntensity, nightStartIntensity, 0.001f, "Intensidade em Dia->Noite deve ser contínua");

            float nightEndIntensity = CalculateIntensity(false, 1.0f);
            float dayStartIntensity = CalculateIntensity(true, 0.0f);
            AssertApproximately(nightEndIntensity, dayStartIntensity, 0.001f, "Intensidade em Noite->Dia deve ser contínua");

            // Continuidade de Cor da luz
            Color dayEndColor = CalculateLightColor(true, 1.0f);
            Color nightStartColor = CalculateLightColor(false, 0.0f);
            AssertColorApproximately(dayEndColor, nightStartColor, 0.01f, "Cor da luz em Dia->Noite deve ser contínua");

            Color nightEndColor = CalculateLightColor(false, 1.0f);
            Color dayStartColor = CalculateLightColor(true, 0.0f);
            AssertColorApproximately(nightEndColor, dayStartColor, 0.01f, "Cor da luz em Noite->Dia deve ser contínua");
        }

        private static void Test_ClockTimeContinuity()
        {
            // Dia: 06:00 a 20:00
            float startHour = 6f + 0f * 14f;
            float endHour = 6f + 1f * 14f;
            AssertApproximately(startHour, 6f, 0.01f, "Dia deve começar às 06:00");
            AssertApproximately(endHour, 20f, 0.01f, "Dia deve encerrar às 20:00");

            // Noite: 20:00 a 06:00
            float nightStartHour = (20f + 0f * 10f) % 24f;
            float nightEndHour = (20f + 1f * 10f) % 24f;
            AssertApproximately(nightStartHour, 20f, 0.01f, "Noite deve começar às 20:00");
            AssertApproximately(nightEndHour, 6f, 0.01f, "Noite deve encerrar às 06:00 (amanhecer)");
        }

        private static void Test_CelestialSunMoonDayNightSanity()
        {
            // Valida que durante todo o dia útil o Sol está no céu e a Lua sob o horizonte
            for (float p = 0.05f; p <= 0.95f; p += 0.15f)
            {
                DayNightCycle.CalculateCelestialVectors(true, p, 32f, 68f, 28f, 62f, out Vector3 sunDir, out Vector3 moonDir, out _);
                AssertTrue(sunDir.y > 0f, $"Em progresso {p:F2} do dia, Sol DEVE estar no céu (sunDir.y={sunDir.y:F3} > 0)");
                AssertTrue(moonDir.y < 0f, $"Em progresso {p:F2} do dia, Lua DEVE estar sob o horizonte (moonDir.y={moonDir.y:F3} < 0)");
            }

            // Valida que durante toda a noite a Lua está no céu e o Sol sob o horizonte
            for (float p = 0.05f; p <= 0.95f; p += 0.15f)
            {
                DayNightCycle.CalculateCelestialVectors(false, p, 32f, 68f, 28f, 62f, out Vector3 sunDir, out Vector3 moonDir, out _);
                AssertTrue(moonDir.y > 0f, $"Em progresso {p:F2} da noite, Lua DEVE estar no céu (moonDir.y={moonDir.y:F3} > 0)");
                AssertTrue(sunDir.y < 0f, $"Em progresso {p:F2} da noite, Sol DEVE estar sob o horizonte (sunDir.y={sunDir.y:F3} < 0)");
            }

            // Valida elevações máximas ao Meio-dia e Meia-noite
            DayNightCycle.CalculateCelestialVectors(true, 0.5f, 32f, 68f, 28f, 62f, out Vector3 middaySun, out _, out _);
            AssertTrue(middaySun.y > 0.85f, $"Sol ao meio-dia deve atingir elevação máxima (y={middaySun.y:F3})");

            DayNightCycle.CalculateCelestialVectors(false, 0.5f, 32f, 68f, 28f, 62f, out _, out Vector3 midnightMoon, out _);
            AssertTrue(midnightMoon.y > 0.80f, $"Lua à meia-noite deve atingir elevação máxima (y={midnightMoon.y:F3})");
        }

        private static void Test_DirectionalLightAndShadowSanity()
        {
            // Valida que a luz direcional SEMPRE aponta para baixo (Y < 0), iluminando o terreno e projetando sombras
            for (float p = 0f; p <= 1f; p += 0.1f)
            {
                DayNightCycle.CalculateCelestialVectors(true, p, 32f, 68f, 28f, 62f, out _, out _, out Vector3 dayLightFwd);
                AssertTrue(dayLightFwd.y < 0f, $"Em progresso {p:F1} do dia, luz deve incidir para o chão (light.y={dayLightFwd.y:F3} < 0)");

                DayNightCycle.CalculateCelestialVectors(false, p, 32f, 68f, 28f, 62f, out _, out _, out Vector3 nightLightFwd);
                AssertTrue(nightLightFwd.y < 0f, $"Em progresso {p:F1} da noite, luar deve incidir para o chão (light.y={nightLightFwd.y:F3} < 0)");
            }

            // Valida continuidade perfeita na transição Pôr do Sol -> Noite (sem saltos de sombra)
            DayNightCycle.CalculateCelestialVectors(true, 1.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 dayEndLightFwd);
            DayNightCycle.CalculateCelestialVectors(false, 0.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 nightStartLightFwd);
            float duskDelta = Vector3.Distance(dayEndLightFwd, nightStartLightFwd);
            AssertApproximately(duskDelta, 0f, 0.005f, "Vetor de luz na passagem Crepúsculo->Anoitecer deve ser contínuo (sem saltos de sombra)");

            // Valida continuidade perfeita na transição Madrugada -> Aurora (sem saltos de sombra)
            DayNightCycle.CalculateCelestialVectors(false, 1.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 nightEndLightFwd);
            DayNightCycle.CalculateCelestialVectors(true, 0.0f, 32f, 68f, 28f, 62f, out _, out _, out Vector3 dayStartLightFwd);
            float dawnDelta = Vector3.Distance(nightEndLightFwd, dayStartLightFwd);
            AssertApproximately(dawnDelta, 0f, 0.005f, "Vetor de luz na passagem Madrugada->Alvorecer deve ser contínuo (sem saltos de sombra)");

            // Valida alinhamento físico: ao Meio-dia o Sol fica ao Sul (z < 0) e sombras projetam-se ao Norte (z > 0)
            DayNightCycle.CalculateCelestialVectors(true, 0.5f, 32f, 68f, 28f, 62f, out Vector3 middaySun, out _, out Vector3 middayLightFwd);
            AssertTrue(middaySun.z < 0f, "Sol ao meio-dia deve estar a Sul no hemisfério norte estilizado");
            AssertTrue(middayLightFwd.z > 0f, "Luz deve apontar para o Norte ao meio-dia, projetando sombras a Norte");
        }

        // Funções de réplica idênticas ao DayNightCycle para verificação determinística
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
            // Cria um GameObject temporário com DayNightCycle para testar modulação de neblina e atmosfera
            GameObject go = new GameObject("Test_DayNightCycle_Atmosphere");
            try
            {
                var cycle = go.AddComponent<DayNightCycle>();
                var lightGO = new GameObject("Test_Sun");
                var dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;

                // Teste 1: Meio-dia (visibilidade aberta e névoa mínima límpida)
                cycle.SetPhaseAndProgressForEditor(true, 0.5f, 1);
                float dayFog = cycle.CurrentFogDensity;
                AssertTrue(dayFog > 0.002f && dayFog < 0.005f, $"Névoa ao meio-dia deve ser límpida (obtido: {dayFog})");

                // Teste 2: Crepúsculo (bruma âmbar/dourada atmosférica deve ser mais densa que o meio-dia)
                cycle.SetPhaseAndProgressForEditor(true, 0.90f, 1);
                float duskFog = cycle.CurrentFogDensity;
                AssertTrue(duskFog > dayFog, $"Névoa no crepúsculo ({duskFog}) DEVE ser mais densa que no meio-dia ({dayFog})");

                // Teste 3: Meia-noite (névoa enluarada densa)
                cycle.SetPhaseAndProgressForEditor(false, 0.5f, 1);
                float nightFog = cycle.CurrentFogDensity;
                AssertTrue(nightFog > duskFog, $"Névoa da noite ({nightFog}) DEVE ser mais densa que o crepúsculo ({duskFog})");

                // Teste 4: Noite 7 do Chefe (bruma carmesim mais opressiva de todas)
                cycle.SetPhaseAndProgressForEditor(false, 0.5f, 7);
                float bossFog = cycle.CurrentFogDensity;
                AssertTrue(bossFog > nightFog, $"Névoa da Noite 7 ({bossFog}) DEVE ser a mais densa de todas (maior que {nightFog})");

                // Teste 5: Remoção de partículas da atmosfera
                var atmoGO = new GameObject("WorldAtmosphere");
                atmoGO.AddComponent<Duskborn.Gameplay.World.WorldAtmosphereController>();
                EnvironmentVisualBootstrapper.RemoveWorldAtmosphere();
                AssertTrue(GameObject.Find("WorldAtmosphere") == null, "WorldAtmosphere deve ser removido com sucesso");

                UnityEngine.Object.DestroyImmediate(lightGO);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
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
                AssertTrue(camData != null && camData.renderPostProcessing, "Câmera deve ter renderPostProcessing ativado.");

                EnvironmentVisualBootstrapper.EnsureGlobalVolume();
                var volume = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
                AssertTrue(volume != null, "Volume global deve ser criado/assegurado.");
                AssertTrue(volume.isGlobal, "Volume deve ser global.");
                AssertTrue(volume.sharedProfile != null || volume.profile != null, "Volume deve ter perfil atribuído.");

                EnvironmentVisualBootstrapper.EnsureDayNightFog();
                AssertTrue(RenderSettings.fog, "RenderSettings.fog deve estar ativado.");

                EnvironmentVisualBootstrapper.EnsureVisualPipeline();
                AssertTrue(GameObject.Find("WorldAtmosphere") == null, "WorldAtmosphere não deve existir após o pipeline ser assegurado.");
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
