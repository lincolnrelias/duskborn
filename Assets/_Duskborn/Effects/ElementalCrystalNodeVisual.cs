using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Loot;
using UnityEngine;

namespace Duskborn.Effects
{
    // Node feedback is independent of combat-effect intensity and bounded by camera distance.
    public sealed class ElementalCrystalNodeVisual : MonoBehaviour
    {
        [SerializeField] private RuneKind element;
        private RuneAura aura;
        private ResourceNode node;
        private Light glow;
        private float nextVisibilityCheck;
        private bool visible = true;
        public RuneKind Element => element;
        public void Configure(RuneKind kind) { element = kind; }
        private void OnEnable()
        {
            node = GetComponent<ResourceNode>();
            if (node != null) node.OnHealthChanged += OnHealth;
            Initialize();
            if (aura != null)
            {
                visible = true; aura.enabled = true;
                foreach (var particles in aura.GetComponentsInChildren<ParticleSystem>()) particles.Play();
            }
        }
        public void Initialize()
        {
            if (element == RuneKind.None || aura != null) return;
            var fx = new GameObject("Bright " + ElementalCrystalCatalog.Element(element) + " crystal particles");
            fx.transform.SetParent(transform, false);
            aura = fx.AddComponent<RuneAura>();
            var bounds = RuneAura.LocalBounds(transform);
            bounds.size = Vector3.Scale(bounds.size, new Vector3(.75f, .85f, .75f));
            aura.Configure(element, 4, bounds, false, 1f);
            var particleShader = Resources.Load<Shader>("Shaders/ElementalCrystalAura");
            if (particleShader != null)
                foreach (var renderer in aura.GetComponentsInChildren<Renderer>())
                    if (renderer.sharedMaterial != null) renderer.sharedMaterial.shader = particleShader;
            var lightObject = new GameObject("Crystal glow"); lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = bounds.center;
            glow = lightObject.AddComponent<Light>(); glow.type = LightType.Point;
            glow.color = RuneCatalog.Color(element); glow.intensity = 1.4f; glow.range = 4f;
            glow.shadows = LightShadows.None; glow.enabled = false;
        }
        private void OnHealth(float hp, float max)
        {
            if (hp > 0) return;
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (aura != null) aura.enabled = false;
            if (glow != null) glow.enabled = false;
            enabled = false;
        }
        private void Update()
        {
            if (Time.time < nextVisibilityCheck || aura == null) return;
            nextVisibilityCheck = Time.time + .45f;
            var camera = Camera.main; if (camera == null) return;
            float distanceSq = (camera.transform.position - transform.position).sqrMagnitude;
            bool nearby = distanceSq < 70f * 70f;
            if (nearby != visible)
            {
                visible = nearby; aura.enabled = nearby;
                foreach (var particles in aura.GetComponentsInChildren<ParticleSystem>())
                    if (nearby) particles.Play(); else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            aura.SetDensity(distanceSq < 35f * 35f ? 1f : .3f);
            if (glow != null) glow.enabled = distanceSq < 22f * 22f;
        }
        private void OnDisable()
        {
            if (node != null) node.OnHealthChanged -= OnHealth;
            if (aura != null)
            {
                aura.enabled = false;
                foreach (var particles in aura.GetComponentsInChildren<ParticleSystem>())
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (glow != null) glow.enabled = false;
        }
    }
}
