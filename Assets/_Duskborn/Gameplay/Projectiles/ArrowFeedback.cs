using System.Collections.Generic;
using Duskborn.Audio;
using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    // Cosmetic only. The authoritative impact supplies flesh/surface classification,
    // so death/despawn order cannot turn a confirmed enemy hit into a dust puff.
    public sealed class ArrowFeedback : MonoBehaviour
    {
        private const int MaxBursts = 24;
        private static readonly List<ArrowFeedback> Bursts = new(MaxBursts);
        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private float retireAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPool() => Bursts.Clear();

        public static TrailRenderer AddTrail(Transform arrow, ProjectileDefinition definition)
        {
            if (!Application.isPlaying || Application.isBatchMode || definition.trailMaterial == null) return null;
            var trail = arrow.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = definition.trailMaterial;
            trail.time = 0.3f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f),
                new Keyframe(0.35f, 0.09f), new Keyframe(1f, 0f));
            trail.numCapVertices = 4;
            trail.numCornerVertices = 2;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(new Color(1.8f, 1.7f, 1.25f), 0),
                new GradientColorKey(new Color(1.2f, 0.7f, 0.25f), 1)
            }, new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.9f, 0.35f),
                new GradientAlphaKey(0f, 1) });
            trail.colorGradient = gradient;
            // Include the launch point before the first physics step so close shots
            // also leave a readable segment instead of starting midway through flight.
            trail.AddPosition(arrow.position);
            return trail;
        }

        public static void Impact(ProjectileDefinition definition, Vector3 point, Vector3 direction,
            bool flesh, string surfaceTag, bool localShooter)
        {
            if (!Application.isPlaying || Application.isBatchMode) return;
            var sound = AudioDatabase.Instance?.Combat.PickHitClip(flesh ? "Flesh" : surfaceTag);
            AudioManager.Instance.PlayAtPoint(sound, point, flesh ? 0.95f : 0.65f,
                3f, 45f, 0.08f, localShooter && flesh ? 0.35f : 1f);
            if (definition.impactParticleMaterial == null) return;
            ArrowFeedback burst = null;
            for (int i = Bursts.Count - 1; i >= 0; i--)
            {
                if (Bursts[i] == null) { Bursts.RemoveAt(i); continue; }
                if (!Bursts[i].gameObject.activeSelf) burst = Bursts[i];
            }
            if (burst == null)
            {
                if (Bursts.Count >= MaxBursts) return;
                var go = new GameObject("Arrow impact burst");
                burst = go.AddComponent<ArrowFeedback>();
                burst.Initialize();
                Bursts.Add(burst);
            }
            burst.Emit(definition.impactParticleMaterial, point, direction, flesh);
        }

        private void Initialize()
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.65f;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.1f));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.velocityScale = 0.07f;
            particleRenderer.lengthScale = 1.4f;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
        }

        private void Emit(Material material, Vector3 point, Vector3 direction, bool flesh)
        {
            transform.position = point;
            gameObject.SetActive(true);
            particleRenderer.sharedMaterial = material;
            particles.Clear();
            particles.Play();
            int count = flesh ? 22 : 10;
            for (int i = 0; i < count; i++)
            {
                Color tint = flesh
                    ? Color.Lerp(new Color(0.25f, 0.012f, 0.018f), new Color(0.85f, 0.055f, 0.025f), Random.value)
                    : Color.Lerp(new Color(0.35f, 0.28f, 0.19f), new Color(0.95f, 0.76f, 0.42f), Random.value);
                var particle = new ParticleSystem.EmitParams {
                    position = point + Random.insideUnitSphere * 0.035f,
                    velocity = -direction * Random.Range(0.6f, 2f) + Random.insideUnitSphere * 1.3f + Vector3.up * 0.6f,
                    startLifetime = Random.Range(0.22f, 0.55f),
                    startSize = flesh ? Random.Range(0.045f, 0.12f) : Random.Range(0.025f, 0.075f),
                    startColor = tint,
                    rotation = Random.Range(0f, 360f)
                };
                particles.Emit(particle, 1);
            }
            retireAt = Time.time + 0.7f;
        }

        private void Update()
        {
            if (Time.time < retireAt) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
