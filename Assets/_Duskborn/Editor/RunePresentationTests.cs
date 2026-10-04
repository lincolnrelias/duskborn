using System;
using System.Reflection;
using Duskborn.Effects;
using Duskborn.Gameplay.Enchanting;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Editor
{
    public static class RunePresentationTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private sealed class Source : IDebuffSource
        {
            public readonly DebuffView[] Views = new DebuffView[9];
            public bool TryGetDebuff(RuneKind kind, out DebuffView view)
            { view = Views[(int)kind]; return view.Stacks > 0; }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void RunAllTests()
        {
            TestClockRefresh(); TestEightIconComposition(); TestActorHealthBars(); TestSurfaceIsolation(); TestParticleBudget();
            Debug.Log("[RunePresentationTests] 5/5 tests passed.");
        }
        private static void TestClockRefresh()
        {
            var time = new DebuffTiming(100, 6, 3);
            Check(time.Remaining(100) == 1 && Mathf.Approximately(time.Remaining(103), .5f), "Clock must use shared server time");
            Check(time.Remaining(106) == 0 && time.Remaining(110) == 0, "Expired clock clamps to zero");
            time = new DebuffTiming(104, 9, 6);
            Check(time.Stacks == 6 && time.Remaining(104) == 1 && Mathf.Approximately(time.Remaining(108.5), .5f), "Reapplication refreshes duration and stacks");
            Check(new DebuffTiming(100, 0, 1).Remaining(10000) == 1, "Untimed statuses remain full");
        }
        private static void TestEightIconComposition()
        {
            var go = new GameObject("Debuff row test", typeof(RectTransform));
            try
            {
                var source = new Source();
                for (int i = 1; i <= 8; i++) source.Views[i] = new DebuffView(i, .5f);
                var row = go.AddComponent<RuneStatusBar>(); row.Initialize(source); row.Refresh();
                var clocks = (Image[])typeof(RuneStatusBar).GetField("_clocks", Private).GetValue(row);
                var counts = (TextMeshProUGUI[])typeof(RuneStatusBar).GetField("_counts", Private).GetValue(row);
                var chips = (RectTransform[])typeof(RuneStatusBar).GetField("_chips", Private).GetValue(row);
                Check(row.HasDebuffs, "Active row visible");
                for (int i = 1; i <= 8; i++)
                {
                    Check(clocks[i].sprite != null && clocks[i].fillClockwise && clocks[i].fillMethod == Image.FillMethod.Radial360 && clocks[i].fillOrigin == (int)Image.Origin360.Top, "Clockwise radial icon for " + (RuneKind)i);
                    Check(clocks[i].fillAmount == .5f && counts[i].text == i.ToString(), "Live time and stack label for " + (RuneKind)i);
                    if (i > 1) Check(chips[i].anchoredPosition.x - chips[i - 1].anchoredPosition.x > chips[i].sizeDelta.x, "Icons must not overlap");
                    source.Views[i] = default;
                }
                row.Refresh(); Check(!row.HasDebuffs, "Empty row must hide its icons");
                source.Views[2] = new DebuffView(12, 1); row.Refresh();
                Check(chips[2].anchoredPosition.x == 0 && counts[2].text == "12" && clocks[2].fillAmount == 1, "Reapplication recenters and refills the icon");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        private static void TestActorHealthBars()
        {
            foreach (string path in new[] { "Assets/_Duskborn/Prefabs/Enemies/Bramblekin.prefab", "Assets/_Duskborn/Prefabs/Player/Player.prefab", "Assets/_Duskborn/Prefabs/Player/Player 1.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(prefab != null, "Actor prefab exists: " + path);
                var actor = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var bar = WorldHealthBar.EnsureForActor(actor.transform);
                    Check(bar != null, "Actor shares world health bar");
                    typeof(WorldHealthBar).GetMethod("Start", Private).Invoke(bar, null);
                    var fill = (Image)typeof(WorldHealthBar).GetField("fill", Private).GetValue(bar);
                    Check(fill.sprite != null && fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal, "Health fill must have a sprite and horizontal geometry");
                    var row = bar.GetComponentInChildren<RuneStatusBar>(true);
                    Check(row != null && row.transform.parent == bar.transform && ((RectTransform)row.transform).anchoredPosition.y > .2f, "Actor debuffs must sit above health in the same canvas");
                    Check(bar.GetComponent<CanvasGroup>().blocksRaycasts == false, "Combat health UI must not intercept clicks");
                }
                finally { UnityEngine.Object.DestroyImmediate(actor); }
            }
        }
        private static void TestSurfaceIsolation()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var source = go.GetComponent<Renderer>(); var original = source.sharedMaterial;
                var surface = go.AddComponent<RuneSurface>(); var strengths = new int[9];
                for (int i = 1; i <= 8; i++) strengths[i] = 3;
                surface.SetEffects(strengths);
                var shell = go.GetComponentInChildren<RuneSurfaceMesh>();
                Check(shell != null && source.sharedMaterial == original, "Shader overlay must preserve authored materials");
                var overlay = shell.GetComponent<Renderer>();
                Check(overlay.sharedMaterial.shader == Resources.Load<Shader>("Shaders/RuneSurface"), "Composite shader included in build");
                Check(overlay.sharedMaterial.GetVector("_RunesA") == new Vector4(3, 3, 3, 3) && overlay.sharedMaterial.GetVector("_RunesB") == new Vector4(3, 3, 3, 3), "All eight effects reach one surface pass");
                foreach (var message in ShaderUtil.GetShaderMessages(overlay.sharedMaterial.shader))
                    Check(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
                Array.Clear(strengths, 0, strengths.Length); surface.SetEffects(strengths);
                Check(!overlay.enabled, "Cleared statuses disable the surface pass");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        private static void TestParticleBudget()
        {
            var root = new GameObject("Mixed rune particle budget");
            try
            {
                float total = 0;
                for (int i = 1; i <= 8; i++)
                {
                    var go = new GameObject("Effect " + i); go.transform.SetParent(root.transform);
                    var aura = go.AddComponent<RuneAura>(); aura.Configure((RuneKind)i, 12, new Bounds(Vector3.zero, Vector3.one), false);
                    aura.SetDensity(1f / Mathf.Sqrt(8));
                    var particles = go.GetComponentInChildren<ParticleSystem>();
                    particles.Simulate(2f, true, true);
                    total += particles.emission.rateOverTime.constant;
                    Check(particles.main.maxParticles <= 80, "Each particle system has a fixed cap");
                }
                Check(total <= 205, "Eight effects must share a bounded emission budget");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
