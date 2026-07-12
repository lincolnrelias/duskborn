using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    // Spawns fading mesh snapshots ("ghosts") behind the character. Call EmitFor(duration).
    public class AfterimageTrail : MonoBehaviour
    {
        [SerializeField] private float spawnInterval = 0.05f;
        [SerializeField] private float fadeDuration  = 0.35f;
        [SerializeField] private Color color         = new(0.45f, 0.8f, 1f, 0.55f);

        private static Material _baseMaterial;
        private Coroutine _emitting;

        public void EmitFor(float duration)
        {
            if (_emitting != null) StopCoroutine(_emitting);
            _emitting = StartCoroutine(EmitRoutine(duration));
        }

        private IEnumerator EmitRoutine(float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                SpawnSnapshot();
                yield return new WaitForSeconds(spawnInterval);
            }
            _emitting = null;
        }

        private void SpawnSnapshot()
        {
            if (_baseMaterial == null) _baseMaterial = GhostMaterial.Create();
            if (_baseMaterial == null) return;

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled) continue;
                var mesh = new Mesh();
                smr.BakeMesh(mesh, true); // scale baked in
                CreateImage(mesh, smr.transform.position, smr.transform.rotation, Vector3.one, ownsMesh: true);
            }

            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
                CreateImage(mf.sharedMesh, mf.transform.position, mf.transform.rotation,
                            mf.transform.lossyScale, ownsMesh: false);
            }
        }

        private void CreateImage(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, bool ownsMesh)
        {
            var go = new GameObject("Afterimage");
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var mat = new Material(_baseMaterial);
            mr.sharedMaterial = mat;

            go.AddComponent<Afterimage>().Init(mat, color, fadeDuration, ownsMesh ? mesh : null);
        }
    }

    internal class Afterimage : MonoBehaviour
    {
        private Material _material;
        private Mesh     _ownedMesh;
        private Color    _startColor;
        private float    _fadeDuration;
        private float    _elapsed;

        public void Init(Material material, Color color, float fadeDuration, Mesh ownedMesh)
        {
            _material     = material;
            _startColor   = color;
            _fadeDuration = Mathf.Max(fadeDuration, 0.01f);
            _ownedMesh    = ownedMesh;
            _material.color = color;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _fadeDuration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            var c = _startColor;
            c.a *= 1f - t;
            _material.color = c;
        }

        private void OnDestroy()
        {
            if (_material  != null) Destroy(_material);
            if (_ownedMesh != null) Destroy(_ownedMesh);
        }
    }
}
