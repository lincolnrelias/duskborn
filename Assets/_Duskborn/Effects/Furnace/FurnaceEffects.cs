using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Duskborn.Effects
{
    /// <summary>Apresentação local do efeito da fornalha. O estado de processamento permanece no PlacedBuilding.</summary>
    [SelectionBase]
    public sealed class FurnaceEffects : MonoBehaviour
    {
        [Header("Materiais & Âncoras")]
        public Material fireMaterial, smokeMaterial, coalMaterial;
        public Transform fireAnchor, smokeAnchor;
        public bool fitAnchorsToModel = true;

        [Header("Fogo")]
        [Range(3, 9)] public int flameCount = 5;
        [Range(0.4f, 2.5f)] public float flameScale = 1f;
        [Range(0.4f, 2.5f)] public float flameHeight = 1f;
        [Range(0.4f, 2.0f)] public float flameSpread = 1f;
        public Color flameTint = new Color(1, .62f, .18f);
        [Range(0f, 3f)] public float coalIntensity = 1f;

        [Header("Fumaça")]
        [Range(0, 80)] public float smokeDensity = 14f;
        [Range(10, 250)] public int maxSmokeParticles = 100;
        [Range(0.1f, 1.2f)] public float smokeSize = 0.32f;
        [Range(0.05f, 1f)] public float smokeAlpha = 0.55f;
        [Range(0.8f, 6f)] public float smokeLifetime = 2.8f;
        [Range(0.1f, 1.5f)] public float smokeSpeed = 0.42f;
        public Color smokeTint = new Color(.43f, .40f, .36f, .35f);
        [Min(0)] public float smokeDelay = .3f;

        [Header("Transições e Iluminação")]
        [Range(0, 3)] public float intensity = 1f;
        [Min(.01f)] public float startupSeconds = .6f, cooldownSeconds = 1.5f;
        [Range(0, 2)] public float lightIntensity = .7f;
        [Min(1)] public float particleDistance = 40, lightDistance = 10;

        private PlacedBuilding owner;
        private ParticleSystem smoke;
        private MeshRenderer[] tongues;
        private Renderer coals;
        private Light warmth;
        private MaterialPropertyBlock properties;
        private bool operating, built, culled;
        private bool supportsHearthLight;
        private float heat, operatingTime, phase, animationTime;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Camera view;
        private float nextCameraCheck;
        private static Mesh tongueMesh;
        private static readonly int Heat = Shader.PropertyToID("_Heat");
        private static readonly int Phase = Shader.PropertyToID("_Phase");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int AnimationTime = Shader.PropertyToID("_AnimationTime");

        public bool IsOperating => operating;
        public float CurrentHeat => heat;
        public float OperatingTime => operatingTime;
        public ParticleSystem SmokeParticleSystem => smoke;
        public bool IsBuilt => built;
        public int LiveParticleCount => smoke != null ? smoke.particleCount : 0;

        // Also used on inert previews. Never mutates the model or shared atlas material.
        public void PrepareModel(GameObject model)
        {
            if (model == null) return;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "Furnace_Flames") r.enabled = false;
                if (r.name == "Furnace_Coals" && coalMaterial != null) r.sharedMaterial = coalMaterial;
            }
        }

        public void Bind(PlacedBuilding building)
        {
            owner = building;
            Build();
            SetOperating(owner != null && owner.IsProcessing);
        }

        public void SetOperating(bool value)
        {
            if (operating == value) return;
            operating = value;
            if (value) operatingTime = 0;
        }

        public void EnsureBuilt()
        {
            if (!built) Build();
        }

        private void Build()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            if (built) return;
            built = true;
            properties = new MaterialPropertyBlock();
            phase = Random.value * 100;
            previousPosition = transform.position;
            previousRotation = transform.rotation;

            if (fireAnchor == null)
            {
                var existingFire = transform.Find("FireAnchor");
                fireAnchor = existingFire != null ? existingFire : Anchor("FireAnchor", new Vector3(0, .84f, .26f));
            }
            if (smokeAnchor == null)
            {
                var existingSmoke = transform.Find("SmokeAnchor");
                smokeAnchor = existingSmoke != null ? existingSmoke : Anchor("SmokeAnchor", new Vector3(0, 2.28f, 0));
            }

            if (owner != null)
            {
                // Measure in the actual imported model's space, including FBX axis conversion.
                foreach (var r in owner.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name == "Furnace_Flames" && fitAnchorsToModel)
                    {
                        var p = r.bounds.center; p.y = r.bounds.min.y;
                        fireAnchor.position = p;
                        r.enabled = false;
                    }
                    if (r.name == "Furnace_PlasteredShell" && fitAnchorsToModel)
                    {
                        var p = r.bounds.center; p.y = r.bounds.max.y - .025f;
                        smokeAnchor.position = p;
                    }
                    if (r.name == "Furnace_Coals") coals = r;
                }
            }

            RebuildTongues();
            BuildSmokeSystem();
            BuildHearthLight();
            ApplyConfiguration();
            ApplyHeat();
        }

        private void RebuildTongues()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            if (fireAnchor == null) return;

            // Remove existing tongues if any
            for (int i = fireAnchor.childCount - 1; i >= 0; i--)
            {
                var child = fireAnchor.GetChild(i);
                if (child.name.StartsWith("Flame tongue"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            int count = Mathf.Clamp(flameCount, 3, 9);
            tongues = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Flame tongue " + i);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(fireAnchor, false);
                go.AddComponent<MeshFilter>().sharedMesh = TongueMesh();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fireMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                tongues[i] = r;
            }
        }

        private void BuildSmokeSystem()
        {
            if (smokeAnchor == null) return;
            var existingSmoke = smokeAnchor.Find("Chimney wisps");
            GameObject smokeObject;
            if (existingSmoke != null)
            {
                smokeObject = existingSmoke.gameObject;
                smoke = smokeObject.GetComponent<ParticleSystem>();
            }
            else
            {
                smokeObject = new GameObject("Chimney wisps");
                smokeObject.hideFlags = HideFlags.DontSave;
                smokeObject.transform.SetParent(smokeAnchor, false);
                smoke = smokeObject.AddComponent<ParticleSystem>();
            }

            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;

            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .105f;
            shape.angle = 5;
            shape.rotation = new Vector3(-90, 0, 0);

            var emission = smoke.emission;
            emission.rateOverTime = 0;

            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .8f, 1, 2.7f));

            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.045f);
            velocity.y = new ParticleSystem.MinMaxCurve(0);
            velocity.z = new ParticleSystem.MinMaxCurve(.015f);

            var noise = smoke.noise;
            noise.enabled = true;
            noise.strength = .055f;
            noise.frequency = .45f;
            noise.scrollSpeed = .16f;
            noise.octaveCount = 1;

            var sheet = smoke.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0, 1);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0);

            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        private void BuildHearthLight()
        {
            if (fireAnchor == null) return;
            var existingLight = fireAnchor.Find("Hearth warmth");
            GameObject lightObject;
            if (existingLight != null)
            {
                lightObject = existingLight.gameObject;
                warmth = lightObject.GetComponent<Light>();
            }
            else
            {
                lightObject = new GameObject("Hearth warmth");
                lightObject.hideFlags = HideFlags.DontSave;
                lightObject.transform.SetParent(fireAnchor, false);
                lightObject.transform.localPosition = new Vector3(0, .13f, 0);
                warmth = lightObject.AddComponent<Light>();
            }
            warmth.type = LightType.Point;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            supportsHearthLight = pipeline != null && pipeline.supportsAdditionalLightShadows &&
                pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel;
            warmth.color = new Color(1, .39f, .085f);
            warmth.range = .65f;
            warmth.shadows = LightShadows.Soft;
            warmth.shadowBias = .025f;
            warmth.shadowNormalBias = .05f;
            warmth.enabled = false;
        }

        public void ApplyConfiguration()
        {
            if (smoke != null)
            {
                var main = smoke.main;
                // Automatically ensure maxParticles accommodates high density settings so it never chokes
                int computedMax = Mathf.Max(maxSmokeParticles, Mathf.CeilToInt(smokeDensity * smokeLifetime * 1.5f) + 16);
                main.maxParticles = computedMax;
                main.startLifetime = new ParticleSystem.MinMaxCurve(smokeLifetime * .82f, smokeLifetime * 1.18f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(smokeSpeed * .82f, smokeSpeed * 1.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(smokeSize * .75f, smokeSize * 1.25f);

                var colors = smoke.colorOverLifetime;
                colors.enabled = true;
                var gradient = new Gradient();
                float peakAlpha = Mathf.Clamp01(smokeTint.a * smokeAlpha * 1.6f);
                gradient.SetKeys(
                    new[] { new GradientColorKey(smokeTint, 0), new GradientColorKey(new Color(.46f, .48f, .49f), 1) },
                    new[] {
                        new GradientAlphaKey(0, 0),
                        new GradientAlphaKey(peakAlpha, .12f),
                        new GradientAlphaKey(peakAlpha * .62f, .52f),
                        new GradientAlphaKey(0, 1)
                    }
                );
                colors.color = gradient;

                var renderer = smoke.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && smokeMaterial != null) renderer.sharedMaterial = smokeMaterial;
            }

            if (tongues != null)
            {
                for (int i = 0; i < tongues.Length; i++)
                {
                    if (tongues[i] == null) continue;
                    float offsetNorm = tongues.Length > 1 ? (float)i / (tongues.Length - 1) : 0.5f;
                    float xPos = (offsetNorm - 0.5f) * 0.28f * flameSpread;
                    float zPos = ((i % 2) * .055f - .025f) * flameSpread;
                    tongues[i].transform.localPosition = new Vector3(xPos, 0, zPos);
                    tongues[i].transform.localRotation = Quaternion.Euler(0, i * 71f, 0);

                    float baseW = (.09f + (i % 2) * .025f) * flameScale;
                    float baseH = (.23f + (i % 3) * .052f) * flameHeight;
                    tongues[i].transform.localScale = new Vector3(baseW, baseH, baseW);
                    if (fireMaterial != null) tongues[i].sharedMaterial = fireMaterial;
                }
            }
        }

        private Transform Anchor(string label, Vector3 position)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        private void Update()
        {
            if (owner != null) SetOperating(owner.IsProcessing);
            if (Application.isPlaying)
            {
                if (Time.unscaledTime >= nextCameraCheck)
                {
                    nextCameraCheck = Time.unscaledTime + .5f;
                    view = Camera.main;
                }
                AdvanceVisuals(Time.deltaTime, view);
            }
        }

        // Allows editor preview to exercise transitions and tick simulation.
        public void AdvanceVisuals(float delta, Camera camera)
        {
            if (!built) Build();
            if ((transform.position - previousPosition).sqrMagnitude > .0001f || Quaternion.Angle(transform.rotation, previousRotation) > .1f)
            {
                if (smoke != null) smoke.Clear(true);
                previousPosition = transform.position;
                previousRotation = transform.rotation;
            }

            float distance = camera == null ? 0f : Vector3.Distance(camera.transform.position, transform.position);
            bool far = camera != null && distance > particleDistance;
            if (far && !culled && smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            culled = far;

            float dt = Mathf.Max(0, delta);
            animationTime += dt;
            operatingTime = operating ? operatingTime + dt : 0;
            heat = Mathf.MoveTowards(heat, operating ? 1 : 0, dt / Mathf.Max(.01f, operating ? startupSeconds : cooldownSeconds));

            bool emit = operating && operatingTime >= smokeDelay && !culled;
            if (smoke != null)
            {
                var emission = smoke.emission;
                emission.rateOverTime = emit ? smokeDensity * heat : 0;
                if (emit && !smoke.isPlaying) smoke.Play(true);
                if (!emit && smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (warmth != null)
            {
                warmth.enabled = supportsHearthLight && heat > .001f && (camera == null || distance < lightDistance);
                warmth.intensity = lightIntensity * intensity * heat * (camera == null ? 1f : Mathf.Clamp01((lightDistance - distance) / 2)) *
                    (.92f + .08f * Mathf.Sin(animationTime * 3.7f + phase));
            }

            ApplyHeat();
        }

        public void SimulateEditor(float delta, Camera camera)
        {
            EnsureBuilt();
            AdvanceVisuals(delta, camera);
            if (smoke != null)
            {
                smoke.Simulate(delta, false, false, false);
            }
        }

        public void ResetSimulation()
        {
            operating = false;
            operatingTime = 0;
            heat = 0;
            animationTime = 0;
            if (smoke != null)
            {
                smoke.Clear(true);
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (warmth != null) warmth.enabled = false;
            ApplyHeat();
        }

        private void ApplyHeat()
        {
            if (tongues != null)
            {
                for (int i = 0; i < tongues.Length; i++)
                {
                    if (tongues[i] == null) continue;
                    tongues[i].enabled = heat > .001f && !culled;
                    properties.Clear();
                    properties.SetFloat(Heat, heat * intensity);
                    properties.SetFloat(Phase, phase + i * 2.39f);
                    properties.SetColor(Tint, flameTint);
                    properties.SetFloat(AnimationTime, animationTime);
                    tongues[i].SetPropertyBlock(properties);
                }
            }
            if (coals != null)
            {
                properties.Clear();
                properties.SetFloat(Heat, heat * intensity * coalIntensity);
                coals.SetPropertyBlock(properties);
            }
        }

        private void OnDisable()
        {
            operating = false;
            operatingTime = heat = 0;
            if (!built) return;
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (warmth != null) warmth.enabled = false;
            ApplyHeat();
        }

        private void OnDestroy()
        {
            if (tongues != null)
            {
                for (int i = 0; i < tongues.Length; i++)
                {
                    if (tongues[i] != null)
                    {
                        if (Application.isPlaying) Destroy(tongues[i].gameObject);
                        else DestroyImmediate(tongues[i].gameObject);
                    }
                }
            }
            if (smoke != null)
            {
                if (Application.isPlaying) Destroy(smoke.gameObject);
                else DestroyImmediate(smoke.gameObject);
            }
            if (warmth != null)
            {
                if (Application.isPlaying) Destroy(warmth.gameObject);
                else DestroyImmediate(warmth.gameObject);
            }
            built = false;
        }

        private bool AnyTongueMissing()
        {
            if (tongues == null) return true;
            for (int i = 0; i < tongues.Length; i++)
                if (tongues[i] == null) return true;
            return false;
        }

        private void OnValidate()
        {
            flameCount = Mathf.Clamp(flameCount, 3, 9);
            flameScale = Mathf.Clamp(flameScale, 0.4f, 2.5f);
            flameHeight = Mathf.Clamp(flameHeight, 0.4f, 2.5f);
            flameSpread = Mathf.Clamp(flameSpread, 0.4f, 2.0f);
            coalIntensity = Mathf.Clamp(coalIntensity, 0f, 3f);
            smokeDensity = Mathf.Max(0f, smokeDensity);
            maxSmokeParticles = Mathf.Clamp(maxSmokeParticles, 10, 300);
            smokeSize = Mathf.Clamp(smokeSize, 0.1f, 1.5f);
            smokeAlpha = Mathf.Clamp01(smokeAlpha);
            smokeLifetime = Mathf.Clamp(smokeLifetime, 0.5f, 6f);
            smokeSpeed = Mathf.Clamp(smokeSpeed, 0.05f, 2f);

#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif

            if (built)
            {
#if UNITY_EDITOR
                if (tongues == null || tongues.Length != flameCount || AnyTongueMissing())
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this != null && built)
                        {
                            RebuildTongues();
                            ApplyConfiguration();
                            ApplyHeat();
                        }
                    };
                    return;
                }
#endif
                ApplyConfiguration();
                ApplyHeat();
            }
        }

        [ContextMenu("Preview/Start")] private void PreviewStart() { Build(); SetOperating(true); }
        [ContextMenu("Preview/Stop")] private void PreviewStop() => SetOperating(false);

        private static Mesh TongueMesh()
        {
            if (tongueMesh != null) return tongueMesh;
            // Crossed subdivided ribbons: readable from oblique views and smoothly curled by the shader.
            var vertices = new Vector3[36]; var uv = new Vector2[36]; var triangles = new int[96];
            int t = 0;
            for (int plane = 0; plane < 2; plane++)
            for (int row = 0; row < 9; row++)
            {
                int n = plane * 18 + row * 2; float y = row / 8f;
                vertices[n] = plane == 0 ? new Vector3(-.5f, y, 0) : new Vector3(0, y, -.5f);
                vertices[n + 1] = plane == 0 ? new Vector3(.5f, y, 0) : new Vector3(0, y, .5f);
                uv[n] = new Vector2(0, y); uv[n + 1] = new Vector2(1, y);
                if (row == 8) continue;
                triangles[t++] = n; triangles[t++] = n + 2; triangles[t++] = n + 1;
                triangles[t++] = n + 1; triangles[t++] = n + 2; triangles[t++] = n + 3;
            }
            tongueMesh = new Mesh { name = "Furnace crossed ribbons", hideFlags = HideFlags.HideAndDontSave };
            tongueMesh.vertices = vertices; tongueMesh.uv = uv; tongueMesh.triangles = triangles;
            tongueMesh.bounds = new Bounds(new Vector3(0, .5f, 0), new Vector3(2, 1.5f, 2));
            return tongueMesh;
        }
    }
}
