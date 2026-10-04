using System;
using System.IO;
using System.Reflection;
using Duskborn.Effects;
using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Equipment;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Duskborn.Editor
{
    /// <summary>Hidden batch-mode GPU capture of production actors, bars, shaders and particles.</summary>
    public static class RuneVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class PreviewSource : IDebuffSource
        {
            public readonly int[] Strengths = new int[9];
            public bool TryGetDebuff(RuneKind kind, out DebuffView view)
            { int i = (int)kind; view = new DebuffView(Strengths[i], .25f + i * .075f); return Strengths[i] > 0; }
        }
        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Capture needs graphics; use Tools/unity.ps1 capture-runes.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Rune capture camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 5.1f, -22);
            camera.transform.rotation = Quaternion.identity; camera.orthographic = true; camera.orthographicSize = 6.15f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .034f, .055f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 60;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .62f, .7f);
            var light = new GameObject("Key light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(35, -35, 0);
            Label("RUNE EFFECTS  /  HEALTH & DEBUFF PRESENTATION", new Vector3(0, 10.95f, 0), new Vector2(14, .4f), .27f);
            Label("Static production prefabs · shader surface + particles · clockwise remaining time", new Vector3(0, 10.52f, 0), new Vector2(14, .3f), .17f);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/Enemies/Bramblekin.prefab");
            for (int panel = 0; panel < 12; panel++)
            {
                Vector3 position = new Vector3((panel % 4 - 1.5f) * 3.65f, 7.1f - (panel / 4) * 3.6f, 0);
                string title = panel < 8 ? ((RuneKind)(panel + 1)).ToString().ToUpperInvariant() : panel == 8 ? "ALL EIGHT / ENEMY" : panel == 9 ? "PLAYER / FOUR STATUSES" : panel == 10 ? "FLAME / WEAPON" : "STORM / WEAPON";
                Label(title, position + Vector3.up * 2.8f, new Vector2(3.5f, .3f), .18f);
                if (panel >= 10) { Weapon(position + Vector3.up * .4f, panel == 10 ? RuneKind.Flame : RuneKind.Storm); continue; }
                var prefab = panel == 9 ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/Player/Player.prefab") : enemyPrefab;
                var actor = UnityEngine.Object.Instantiate(prefab); actor.name = title;
                actor.transform.position = position; actor.transform.rotation = Quaternion.Euler(0, 180, 0);
                foreach (var animator in actor.GetComponentsInChildren<Animator>()) animator.enabled = false;
                var bounds = RuneAura.LocalBounds(actor.transform);
                var source = new PreviewSource();
                if (panel < 8) source.Strengths[panel + 1] = 6;
                else if (panel == 8) for (int i = 1; i <= 8; i++) source.Strengths[i] = i + 1;
                else { source.Strengths[1] = 3; source.Strengths[3] = 1; source.Strengths[4] = 5; source.Strengths[6] = 4; }
                actor.AddComponent<RuneSurface>().SetEffects(source.Strengths);
                int active = panel < 8 ? 1 : panel == 8 ? 8 : 4;
                for (int i = 1; i <= 8; i++) if (source.Strengths[i] > 0)
                {
                    Bounds region = bounds;
                    if (active > 1)
                    {
                        float band = ((i - 1) * .618034f) % 1f;
                        region.center += Vector3.up * (band - .5f) * bounds.size.y * .65f;
                        region.size = new Vector3(bounds.size.x, bounds.size.y * .45f, bounds.size.z);
                    }
                    var go = new GameObject("Preview " + (RuneKind)i); go.transform.SetParent(actor.transform, false);
                    var aura = go.AddComponent<RuneAura>(); aura.Configure((RuneKind)i, source.Strengths[i], region, false);
                    aura.SetDensity(1f / Mathf.Sqrt(active)); Simulate(aura);
                }
                var bar = WorldHealthBar.EnsureForActor(actor.transform);
                typeof(WorldHealthBar).GetMethod("Start", Private).Invoke(bar, null);
                bar.transform.rotation = camera.transform.rotation;
                bar.transform.position = position + Vector3.up * 1.98f;
                var scale = actor.transform.lossyScale; bar.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                var row = bar.GetComponentInChildren<RuneStatusBar>();
                typeof(RuneStatusBar).GetField("_source", Private).SetValue(row, source); row.Refresh();
                bar.GetComponent<CanvasGroup>().alpha = 1;
                var fill = (Image)typeof(WorldHealthBar).GetField("fill", Private).GetValue(bar);
                fill.fillAmount = .72f;
                fill.color = (Color)typeof(WorldHealthBar).GetMethod("GetBarColor", Private).Invoke(bar, new object[] { .72f });
                var ghost = (Image)typeof(WorldHealthBar).GetField("ghostFill", Private).GetValue(bar); ghost.enabled = true; ghost.fillAmount = .91f;
                ((TextMeshProUGUI)typeof(WorldHealthBar).GetField("_healthText", Private).GetValue(bar)).text = "72 / 100";
                ((TextMeshProUGUI)typeof(WorldHealthBar).GetField("nameLabel", Private).GetValue(bar)).text = panel == 9 ? "Explorer" : "Bramblekin";
            }
            var render = new RenderTexture(1500, 1200, 24, RenderTextureFormat.ARGB32); render.Create();
            camera.targetTexture = render; Canvas.ForceUpdateCanvases(); camera.Render();
            var texture = new Texture2D(1500, 1200, TextureFormat.RGB24, false);
            RenderTexture.active = render; texture.ReadPixels(new Rect(0, 0, 1500, 1200), 0, 0); texture.Apply();
            string output = Path.GetFullPath("Artifacts/RunePresentation/runes-overview.png"); Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, texture.EncodeToPNG());
            foreach (var shader in new[] { Resources.Load<Shader>("Shaders/RuneSurface"), Resources.Load<Shader>("Shaders/RuneAura") })
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new InvalidOperationException(message.message);
            RenderTexture.active = null; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(render);
            Debug.Log("[RuneVisualCapture] Capture succeeded: " + output);
        }
        private static void Weapon(Vector3 position, RuneKind kind)
        {
            WeaponDefinition definition = null;
            foreach (var weapon in Resources.LoadAll<WeaponDefinition>(""))
                if (weapon.Prefab != null && (definition == null || weapon.Id.ToLowerInvariant().Contains("bloodblade"))) definition = weapon;
            if (definition == null) throw new InvalidOperationException("No weapon prefab available.");
            var go = UnityEngine.Object.Instantiate(definition.Prefab); go.transform.position = position;
            Bounds bounds = RuneAura.LocalBounds(go.transform);
            go.transform.localScale *= 1.55f / Mathf.Max(.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            go.transform.rotation = Quaternion.Euler(0, 0, -35);
            var aura = go.AddComponent<RuneAura>(); aura.Configure(kind, 3, bounds); Simulate(aura);
            Label("Tier III · persistent etched surface", position - Vector3.up * .6f, new Vector2(3.5f, .3f), .14f);
        }
        private static void Simulate(RuneAura aura)
        {
            typeof(RuneAura).GetMethod("Update", Private).Invoke(aura, null);
            foreach (var particles in aura.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(.63f, true, true);
        }
        private static void Label(string text, Vector3 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Canvas)); go.transform.position = position;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; ((RectTransform)go.transform).sizeDelta = size;
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false); label.rectTransform.sizeDelta = size; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(.85f, .9f, 1); label.text = text;
        }
    }
}
