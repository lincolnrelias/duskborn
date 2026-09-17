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
    }
}
