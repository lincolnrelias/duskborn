using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    // Spawns fading mesh snapshots ("ghosts") behind the character using object pooling and MaterialPropertyBlock.
    public class AfterimageTrail : MonoBehaviour
    {
        [SerializeField] private float spawnInterval = 0.05f;
        [SerializeField] private float fadeDuration  = 0.35f;
        [SerializeField] private Color color         = new(0.45f, 0.8f, 1f, 0.55f);

        private static Material _baseMaterial;
        private static readonly Stack<Afterimage> _pool = new();
        private static Transform _container;
        private WaitForSeconds _waitSpawnInterval;
        private Coroutine _emitting;

        private void Awake()
        {
            _waitSpawnInterval = new WaitForSeconds(spawnInterval);
            if (_container == null)
            {
                var go = new GameObject("Afterimage_Container");
                _container = go.transform;
            }
        }

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
                yield return _waitSpawnInterval;
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
                var afterimage = GetOrCreate();
                afterimage.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                afterimage.transform.localScale = Vector3.one;
                afterimage.BakeFrom(smr);
                afterimage.Init(_baseMaterial, color, fadeDuration);
            }

            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
                var afterimage = GetOrCreate();
                afterimage.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                afterimage.transform.localScale = mf.transform.lossyScale;
                afterimage.SetMesh(mf.sharedMesh);
                afterimage.Init(_baseMaterial, color, fadeDuration);
            }
        }

        private static Afterimage GetOrCreate()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Pop();
                if (item != null && item.gameObject != null)
                {
                    item.gameObject.SetActive(true);
                    return item;
                }
            }

            var go = new GameObject("Afterimage");
            if (_container != null) go.transform.SetParent(_container);
            var ai = go.AddComponent<Afterimage>();
            return ai;
        }

        internal static void ReturnToPool(Afterimage item)
        {
            if (item == null || item.gameObject == null) return;
            item.gameObject.SetActive(false);
            _pool.Push(item);
        }
    }

    internal class Afterimage : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MeshFilter            _mf;
        private MeshRenderer          _mr;
        private Mesh                  _bakedMesh;
        private MaterialPropertyBlock _propBlock;
        private Color                 _startColor;
        private float                 _fadeDuration;
        private float                 _elapsed;

        private void Awake()
        {
            _mf = gameObject.AddComponent<MeshFilter>();
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.shadowCastingMode = ShadowCastingMode.Off;
            _bakedMesh = new Mesh { name = "Afterimage_BakedMesh" };
            _propBlock = new MaterialPropertyBlock();
        }

        public void BakeFrom(SkinnedMeshRenderer smr)
        {
            smr.BakeMesh(_bakedMesh, true);
            _mf.sharedMesh = _bakedMesh;
        }

        public void SetMesh(Mesh mesh)
        {
            _mf.sharedMesh = mesh;
        }

        public void Init(Material material, Color color, float fadeDuration)
        {
            _startColor   = color;
            _fadeDuration = Mathf.Max(fadeDuration, 0.01f);
            _elapsed      = 0f;

            _mr.sharedMaterial = material;
            _propBlock.SetColor(BaseColorId, color);
            _mr.SetPropertyBlock(_propBlock);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _fadeDuration;
            if (t >= 1f)
            {
                AfterimageTrail.ReturnToPool(this);
                return;
            }
            var c = _startColor;
            c.a *= 1f - t;
            _propBlock.SetColor(BaseColorId, c);
            _mr.SetPropertyBlock(_propBlock);
        }

        private void OnDestroy()
        {
            if (_bakedMesh != null) Destroy(_bakedMesh);
        }
    }
}
