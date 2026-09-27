using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Duskborn.Effects
{
    /// <summary>Local presentation only. Building snapshots remain the authority for processing.</summary>
    public sealed class FurnaceEffects : MonoBehaviour
    {
        public Material fireMaterial, smokeMaterial, coalMaterial;
        public Transform fireAnchor, smokeAnchor;
        public bool fitAnchorsToModel = true;
        [Min(.01f)] public float startupSeconds = .6f, cooldownSeconds = 1.5f;
        [Min(0)] public float smokeDelay = .3f;
        [Range(0, 10)] public float smokeDensity = 5;
        [Range(0, 2)] public float intensity = 1;
        public Color flameTint = new Color(1, .62f, .18f);
        public Color smokeTint = new Color(.43f, .40f, .36f, .24f);
        [Min(1)] public float particleDistance = 40, lightDistance = 10;
        [Range(0, 2)] public float lightIntensity = .7f;
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

        // Also used on inert previews. Never mutates the model or shared atlas material.
        public void PrepareModel(GameObject model)
        {
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

        private void Build()
        {
            if (built) return;
            built = true;
            properties = new MaterialPropertyBlock();
            phase = Random.value * 100;
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            if (fireAnchor == null) fireAnchor = Anchor("FireAnchor", new Vector3(0, .84f, .26f));
            if (smokeAnchor == null) smokeAnchor = Anchor("SmokeAnchor", new Vector3(0, 2.28f, 0));
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
            tongues = new MeshRenderer[5];
            for (int i = 0; i < tongues.Length; i++)
            {
                var go = new GameObject("Flame tongue " + i);
                go.transform.SetParent(fireAnchor, false);
                go.transform.localPosition = new Vector3((i - 2) * .067f, 0, (i % 2) * .055f - .025f);
                go.transform.localRotation = Quaternion.Euler(0, i * 71, 0);
                go.transform.localScale = new Vector3(.09f + (i % 2) * .025f, .23f + (i % 3) * .052f, .09f);
                go.AddComponent<MeshFilter>().sharedMesh = TongueMesh();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fireMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                tongues[i] = r;
            }
            var smokeObject = new GameObject("Chimney wisps");
            smokeObject.transform.SetParent(smokeAnchor, false);
            smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false; main.loop = true; main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.3f, 3.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.36f, .48f);
            main.startSize = new ParticleSystem.MinMaxCurve(.18f, .25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .105f; shape.angle = 5; shape.rotation = new Vector3(-90, 0, 0);
            var emission = smoke.emission; emission.rateOverTime = 0;
            var size = smoke.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .8f, 1, 2.7f));
            var colors = smoke.colorOverLifetime; colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(smokeTint, 0), new GradientColorKey(new Color(.46f, .48f, .49f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(smokeTint.a, .12f), new GradientAlphaKey(smokeTint.a * .55f, .5f), new GradientAlphaKey(0, 1) });
            colors.color = gradient;
            var velocity = smoke.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.045f); velocity.y = new ParticleSystem.MinMaxCurve(0); velocity.z = new ParticleSystem.MinMaxCurve(.015f);
            var noise = smoke.noise; noise.enabled = true; noise.strength = .055f; noise.frequency = .45f; noise.scrollSpeed = .16f; noise.octaveCount = 1;
            var sheet = smoke.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0, 1); sheet.startFrame = new ParticleSystem.MinMaxCurve(0);
            var renderer = smoke.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            var lightObject = new GameObject("Hearth warmth"); lightObject.transform.SetParent(fireAnchor, false);
            lightObject.transform.localPosition = new Vector3(0, .13f, 0);
            warmth = lightObject.AddComponent<Light>(); warmth.type = LightType.Point;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            supportsHearthLight = pipeline != null && pipeline.supportsAdditionalLightShadows &&
                pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel;
            warmth.color = new Color(1, .39f, .085f); warmth.range = .65f;
            warmth.shadows = LightShadows.Soft; warmth.shadowBias = .025f; warmth.shadowNormalBias = .05f;
            warmth.enabled = false;
            ApplyHeat();
        }

        private Transform Anchor(string label, Vector3 position)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            go.transform.localPosition = position; return go.transform;
        }

        private void Update()
        {
            if (owner != null) SetOperating(owner.IsProcessing);
            if (Time.unscaledTime >= nextCameraCheck)
            {
                nextCameraCheck = Time.unscaledTime + .5f;
                view = Camera.main;
            }
            AdvanceVisuals(Time.deltaTime, view);
        }

        // Allows the isolated editor preview to exercise transitions without ticking crafting.
        public void AdvanceVisuals(float delta, Camera camera)
        {
            if (!built) return;
            if ((transform.position - previousPosition).sqrMagnitude > .0001f || Quaternion.Angle(transform.rotation, previousRotation) > .1f)
            {
                smoke.Clear(true);
                previousPosition = transform.position; previousRotation = transform.rotation;
            }
            float distance = camera == null ? float.PositiveInfinity : Vector3.Distance(camera.transform.position, transform.position);
            bool far = distance > particleDistance;
            if (far && !culled) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            culled = far;
            float dt = Mathf.Max(0, delta);
            animationTime += dt;
            operatingTime = operating ? operatingTime + dt : 0;
            heat = Mathf.MoveTowards(heat, operating ? 1 : 0, dt / Mathf.Max(.01f, operating ? startupSeconds : cooldownSeconds));
            bool emit = operating && operatingTime >= smokeDelay && !culled;
            var emission = smoke.emission; emission.rateOverTime = emit ? smokeDensity * heat : 0;
            if (emit && !smoke.isPlaying) smoke.Play(true);
            if (!emit && smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            warmth.enabled = supportsHearthLight && heat > .001f && distance < lightDistance;
            warmth.intensity = lightIntensity * intensity * heat * Mathf.Clamp01((lightDistance - distance) / 2) *
                (.92f + .08f * Mathf.Sin(animationTime * 3.7f + phase));
            ApplyHeat();
        }

        private void ApplyHeat()
        {
            for (int i = 0; i < tongues.Length; i++)
            {
                tongues[i].enabled = heat > .001f && !culled;
                properties.Clear(); properties.SetFloat(Heat, heat * intensity);
                properties.SetFloat(Phase, phase + i * 2.39f); properties.SetColor(Tint, flameTint);
                properties.SetFloat(AnimationTime, animationTime);
                tongues[i].SetPropertyBlock(properties);
            }
            if (coals != null)
            {
                properties.Clear(); properties.SetFloat(Heat, heat * intensity);
                coals.SetPropertyBlock(properties);
            }
        }

        private void OnDisable()
        {
            operating = false; operatingTime = heat = 0;
            if (!built) return;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            warmth.enabled = false; ApplyHeat();
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
