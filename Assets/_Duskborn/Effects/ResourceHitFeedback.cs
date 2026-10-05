using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    /// <summary>World-space, bounded node chips and dust that survive node depletion.</summary>
    public sealed class ResourceHitFeedback : MonoBehaviour
    {
        public const int SiteCapacity = 12;
        private static ResourceHitFeedback _instance;
        private readonly ParticleSystem[] _chips = new ParticleSystem[SiteCapacity];
        private readonly ParticleSystem[] _dust = new ParticleSystem[SiteCapacity];
        private Mesh _chipMesh;
        private Material _chipMaterial, _dustMaterial;
        private Texture2D _dustTexture;
        private int _nextSite;
        public static GameObject Root => _instance != null ? _instance.gameObject : null;

        public static void Emit(Vector3 point, Vector3 outward, string surface, bool foliage, bool depleted)
        {
            if (_instance == null)
            {
                var root = new GameObject("Resource hit feedback pool");
                _instance = root.AddComponent<ResourceHitFeedback>();
                _instance.Initialize();
            }
            _instance.Burst(point, outward, surface, foliage, depleted);
        }

        private void Initialize()
        {
            // The existing Resources shader is explicitly included in player builds.
            var shader = Resources.Load<Shader>("Shaders/RuneAura")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null) return;
            _chipMaterial = new Material(shader) { name = "Resource chip material" };
            _dustMaterial = new Material(shader) { name = "Resource soft dust material", renderQueue = 3000 };
            if (_dustMaterial.HasProperty("_Surface")) _dustMaterial.SetFloat("_Surface", 1f);
            if (_dustMaterial.HasProperty("_Blend")) _dustMaterial.SetFloat("_Blend", 0f);
            if (_dustMaterial.HasProperty("_SrcBlend")) _dustMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_dustMaterial.HasProperty("_DstBlend")) _dustMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (_dustMaterial.HasProperty("_ZWrite")) _dustMaterial.SetFloat("_ZWrite", 0f);
            if (_dustMaterial.HasProperty("_Surface")) _dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _dustTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Resource dust falloff", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float radius = new Vector2((x + .5f) / 16f - 1f, (y + .5f) / 16f - 1f).magnitude;
                _dustTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 2f)));
            }
            _dustTexture.Apply(false, true);
            _dustMaterial.SetTexture("_BaseMap", _dustTexture);
            if (_dustMaterial.HasProperty("_MainTex")) _dustMaterial.SetTexture("_MainTex", _dustTexture);
            Vector3[] corners = { Vector3.up, Vector3.down * .6f, Vector3.left, Vector3.right * .7f, Vector3.forward, Vector3.back * .8f };
            int[] faces = { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
            var vertices = new Vector3[faces.Length]; var indices = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = corners[faces[i]]; indices[i] = i; }
            _chipMesh = new Mesh { name = "Faceted resource chips", vertices = vertices, triangles = indices };
            _chipMesh.RecalculateNormals(); _chipMesh.RecalculateBounds();
            for (int i = 0; i < SiteCapacity; i++)
            {
                _chips[i] = MakeParticles("Material chips", false);
                _dust[i] = MakeParticles("Contact dust", true);
            }
        }

        private ParticleSystem MakeParticles(string label, bool dust)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = dust ? 20 : 32;
            main.gravityModifier = dust ? .08f : 1.2f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dust ? _dustMaterial : _chipMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.renderMode = dust ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Mesh;
            if (!dust) renderer.mesh = _chipMesh;
            var alpha = particles.colorOverLifetime; alpha.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(dust ? .5f : 1f, 0), new GradientAlphaKey(dust ? .25f : 1f, .45f), new GradientAlphaKey(0, 1) });
            alpha.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, dust
                ? new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.3f, 1f), new Keyframe(1, 1.7f))
                : new AnimationCurve(new Keyframe(0, 1), new Keyframe(.7f, 1), new Keyframe(1, 0)));
            var rotation = particles.rotationOverLifetime; rotation.enabled = !dust;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            return particles;
        }

        private void Burst(Vector3 point, Vector3 outward, string surface, bool foliage, bool depleted)
        {
            int site = _nextSite++ % SiteCapacity;
            var chips = _chips[site]; var dust = _dust[site];
            if (chips == null || dust == null) return;
            chips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            chips.transform.position = dust.transform.position = point;
            chips.Play(); dust.Play();
            bool wood = surface == "Tree" || surface == "Wood";
            bool metal = surface == "Metal" || surface == "Ore";
            Color color = foliage ? new Color(.3f, .48f, .18f) : wood ? new Color(.48f, .29f, .13f)
                : metal ? new Color(.42f, .45f, .5f) : new Color(.55f, .51f, .44f);
            outward = outward.sqrMagnitude > .001f ? outward.normalized : Vector3.up;
            int count = depleted ? 24 : foliage ? 8 : 12;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = outward * Random.Range(.7f, 1.8f) + Vector3.up * Random.Range(.6f, 1.6f) + Random.insideUnitSphere * .9f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = point + Random.insideUnitSphere * .045f,
                    velocity = velocity * (depleted ? 1.35f : 1f),
                    startLifetime = Random.Range(.25f, .65f), startSize = Random.Range(.025f, .065f),
                    startColor = color * Random.Range(.85f, 1.2f), rotation = Random.Range(0f, 360f)
                };
                // Short warm sparks mixed with stone fragments for ore contact.
                if (metal && i % 4 == 0) { particle.startColor = new Color(1f, .75f, .32f); particle.startSize *= .45f; particle.startLifetime *= .6f; }
                chips.Emit(particle, 1);
            }
            for (int i = 0; i < (depleted ? 12 : 5); i++)
                dust.Emit(new ParticleSystem.EmitParams { position = point + Random.insideUnitSphere * .06f,
                    velocity = outward * Random.Range(.1f, .45f) + Vector3.up * .35f + Random.insideUnitSphere * .15f,
                    startSize = Random.Range(.12f, .25f) * (depleted ? 1.4f : 1f), startLifetime = Random.Range(.3f, .6f),
                    startColor = Color.Lerp(color, new Color(.65f, .6f, .5f), .4f) }, 1);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            DestroyOwned(_chipMesh); DestroyOwned(_chipMaterial); DestroyOwned(_dustMaterial); DestroyOwned(_dustTexture);
        }

        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
