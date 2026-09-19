using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Duskborn.Gameplay.World;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Duskborn.Core
{
    /// <summary>
    /// Bootstrapper visual automático e defensivo.
    /// Garante que o pipeline de renderização (Post-Processing na câmera, Global Volume com SampleSceneProfile,
    /// e Neblina inicial) esteja ativo e configurado tanto no Editor quanto no PlayMode sem congelamentos.
    /// </summary>
    public static class EnvironmentVisualBootstrapper
    {
        private const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnSceneLoadedRuntime()
        {
            EnsureVisualPipeline();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                {
                    EnsureVisualPipeline();
                }
            };
        }
#endif

        public static void EnsureVisualPipeline()
        {
            EnsureCameraPostProcessing();
            EnsureGlobalVolume();
            EnsureDayNightFog();
            RemoveWorldAtmosphere();
        }

        public static void EnsureCameraPostProcessing(Camera targetCam = null)
        {
            Camera cam = targetCam != null ? targetCam : Camera.main;
            if (cam == null)
            {
                cam = Object.FindAnyObjectByType<Camera>();
            }

            if (cam != null)
            {
                cam.depthTextureMode |= DepthTextureMode.Depth;
                var camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                    camData.volumeLayerMask = ~0; // Enxerga todas as camadas de Volume
                }
            }
        }

        public static void EnsureGlobalVolume()
        {
            Volume globalVolume = null;
            var allVolumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
            foreach (var vol in allVolumes)
            {
                if (vol != null && vol.isGlobal)
                {
                    globalVolume = vol;
                    break;
                }
            }

            if (globalVolume == null)
            {
                GameObject volGO = GameObject.Find("Global Volume");
                if (volGO == null)
                {
                    volGO = new GameObject("Global Volume");
                }
                globalVolume = volGO.GetComponent<Volume>();
                if (globalVolume == null)
                {
                    globalVolume = volGO.AddComponent<Volume>();
                }
                globalVolume.isGlobal = true;
                globalVolume.weight = 1f;
            }

            if (globalVolume.sharedProfile == null && globalVolume.profile == null)
            {
                VolumeProfile loadedProfile = null;
#if UNITY_EDITOR
                loadedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
#endif
                if (loadedProfile == null)
                {
                    loadedProfile = Resources.Load<VolumeProfile>("SampleSceneProfile");
                }

                if (loadedProfile != null)
                {
                    globalVolume.sharedProfile = loadedProfile;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(globalVolume);
                    }
#endif
                }
                else
                {
                    // Fallback procedural de perfil URP para garantir efeitos sempre visíveis
                    var runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    runtimeProfile.name = "SampleSceneProfile_Procedural";

                    if (!runtimeProfile.TryGet<Bloom>(out var bloom))
                        bloom = runtimeProfile.Add<Bloom>(true);
                    bloom.threshold.Override(0.85f);
                    bloom.intensity.Override(0.45f);
                    bloom.scatter.Override(0.65f);

                    if (!runtimeProfile.TryGet<Vignette>(out var vig))
                        vig = runtimeProfile.Add<Vignette>(true);
                    vig.intensity.Override(0.22f);
                    vig.smoothness.Override(0.35f);

                    if (!runtimeProfile.TryGet<ColorAdjustments>(out var ca))
                        ca = runtimeProfile.Add<ColorAdjustments>(true);
                    ca.postExposure.Override(0.1f);
                    ca.contrast.Override(10f);
                    ca.saturation.Override(8f);

                    if (!runtimeProfile.TryGet<Tonemapping>(out var tm))
                        tm = runtimeProfile.Add<Tonemapping>(true);
                    tm.mode.Override(TonemappingMode.ACES);

                    globalVolume.profile = runtimeProfile;
                }
            }
        }

        public static void EnsureDayNightFog()
        {
            // Ativa neblina inicial
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            if (RenderSettings.fogDensity <= 0.0001f)
            {
                RenderSettings.fogDensity = 0.0038f;
            }
        }

        public static void RemoveWorldAtmosphere()
        {
            var atmos = Object.FindObjectsByType<WorldAtmosphereController>(FindObjectsInactive.Include);
            for (int i = 0; i < atmos.Length; i++)
            {
                if (atmos[i] != null && atmos[i].gameObject != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(atmos[i].gameObject);
                    else
                        Object.DestroyImmediate(atmos[i].gameObject);
                }
            }

            GameObject atmoGO = GameObject.Find("WorldAtmosphere");
            if (atmoGO != null)
            {
                atmoGO.name = "WorldAtmosphere_Removed";
                if (Application.isPlaying)
                    Object.Destroy(atmoGO);
                else
                    Object.DestroyImmediate(atmoGO);
            }
        }
    }
}
