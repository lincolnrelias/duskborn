using System;
using System.IO;
using Duskborn.Effects;
using Duskborn.Gameplay.Enchanting;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Duskborn.Editor
{
    public static class ElementalCrystalCapture
    {
        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Use capture-crystals with graphics enabled.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQualityPipeline = QualitySettings.renderPipeline;
            // Neutral forward URP inspection; transient copies preserve production assets.
            var pipeline = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
            var renderer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
            renderer.rendererFeatures.Clear(); renderer.renderingMode = RenderingMode.Forward;
            pipeline.useSRPBatcher = false;
            var pipelineData = new SerializedObject(pipeline);
            pipelineData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            UniversalRenderPipelineDebugDisplaySettings.Instance.Reset();
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.38f, .45f, .52f);
            var camera = new GameObject("Crystal capture camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 11f, -14); camera.transform.LookAt(new Vector3(0, .5f, 2.9f));
            camera.orthographic = true; camera.orthographicSize = 4.8f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.028f, .038f, .06f);
            var light = new GameObject("Crystal key", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(35, -40, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.position = Vector3.down * .03f; floor.transform.localScale = new Vector3(2, 1, 1.2f);
            var ground = new Material(Shader.Find("Universal Render Pipeline/Lit")); ground.color = new Color(.07f, .1f, .105f); floor.GetComponent<Renderer>().sharedMaterial = ground;
            for (int index = 0; index < 8; index++)
            {
                RuneKind kind = (RuneKind)(index + 1); Vector3 position = new Vector3((index % 4 - 1.5f) * 3.25f, 0, (index / 4) * 5.8f);
                var node = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ElementalCrystalBuilder.NodePath(kind)));
                node.transform.position = position;
                node.transform.rotation = Quaternion.Euler(0, kind == RuneKind.Radiance ? 180 : 25, 0);
                foreach (var canvas in node.GetComponentsInChildren<Canvas>()) canvas.gameObject.SetActive(false);
                var visual = node.GetComponent<ElementalCrystalNodeVisual>(); visual.Initialize();
                foreach (var particles in node.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(.75f, true, true);
                string element = ElementalCrystalCatalog.Element(kind);
                Label(char.ToUpperInvariant(element[0]) + element.Substring(1) + " Crystal", position + new Vector3(0, .05f, -1.1f), .22f, camera);
                Label(kind + " runestones", position + new Vector3(0, .05f, -1.45f), .15f, camera);
                var pickup = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ElementalCrystalBuilder.DropPath(kind)));
                pickup.transform.position = position + new Vector3(1.1f, .22f, -.5f); pickup.transform.rotation = Quaternion.Euler(10, 0, 25);
            }
            var render = new RenderTexture(1800, 1050, 24, RenderTextureFormat.ARGB32); render.Create();
            // Camera.Render falls back to the built-in renderer in immediate batch captures.
            // Submit through URP so this checks the actual production material passes.
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
            var texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); texture.Apply();
            string output = Path.GetFullPath("Artifacts/ElementalCrystals/crystals-overview.png"); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, texture.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(render);
            foreach (var shader in new[] { Resources.Load<Shader>("Shaders/RuneAura"), Resources.Load<Shader>("Shaders/ElementalCrystalAura"), Shader.Find("Universal Render Pipeline/Lit") })
                if (shader != null) foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new InvalidOperationException(message.message);
            Debug.Log("[ElementalCrystalCapture] Capture succeeded: " + output);
            GraphicsSettings.defaultRenderPipeline = previousPipeline;
            QualitySettings.renderPipeline = previousQualityPipeline;
        }
        private static void Label(string text, Vector3 position, float size, Camera camera)
        {
            var go = new GameObject(text, typeof(TextMeshPro)); go.transform.position = position; go.transform.rotation = camera.transform.rotation;
            var label = go.GetComponent<TextMeshPro>(); label.text = text; label.fontSize = size * 10; label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.9f, .94f, 1); label.rectTransform.sizeDelta = new Vector2(3, .35f);
        }
    }
}
