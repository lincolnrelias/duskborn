using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    /// <summary>
    /// Complete Diablo-style visual effect for items dropped on the ground:
    /// - Layer 1: ground contact point with a bright hotspot, concentric ripples, and a diffuse pool.
    /// - Layer 2: continuously rising vertical plasma column (animated shader UVs, white incandescent core, Gaussian falloff).
    /// - Layer 3: motes / sparks rising along the beam toward the sky (Rare, Epic, Legendary).
    /// - Layer 4: initial impact flash / spike settling into a stable column.
    /// - Layer 5: soft point light ("dim light") with organic pulsing.
    /// - Layer 6: outline and animated gradient wave shader on the item mesh (Uncommon and above).
    /// 
    /// IMPORTANT: All visuals are isolated in an independent world root GameObject (_vfxRoot)
    /// with fixed scale (1, 1, 1), making them fully immune to arbitrarily scaled prefabs
    /// such as iron (20x20x20) and wood (15x30x30).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class DroppedItemVisuals : MonoBehaviour
    {
        [Header("Tier Configuration")]
        [SerializeField] private ItemRarity currentRarity = ItemRarity.Common;

        private GameObject      _vfxRoot;
        private Light           _pointLight;
        private GameObject      _beamObject;
        private Transform       _beamTransform;
        private Mesh            _beamMesh;

        private GameObject      _haloObject;
        private Transform       _haloTransform;
        private Mesh            _haloMesh;

        private ParticleSystem  _particles;
        private Rigidbody       _rb;

        private readonly List<GameObject> _overlayObjects = new();
        private static Shader             _waveOverlayShader;
        private static Dictionary<ItemRarity, Material> _sharedWaveMaterials;

        private float _baseIntensity;
        private float _baseRange;
        private float _pulseOffset;
        private float _beamYaw;
        private float _haloYaw;
        private bool  _isSettled;
        private bool _isCollecting;
        private Vector3 _settledPosition;
        private float _spawnTime;
        private float _arrivalTime = float.NegativeInfinity;
        private float _lastGroundContact = float.NegativeInfinity;
        private Vector3 _hoverVelocity;
        private Quaternion _catchRotation;
        private Quaternion _hoverRotation;
        private static PhysicsMaterial _dropContactMaterial;

        // Low air drag preserves the launch; extra gravity gives the descent weight.
        private const float AirDrag = 0.08f;
        private const float GravityScale = 1.75f;
        private bool  _isSetup;

        // Terrain anchoring.
        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _groundPoint;
        private bool    _hasGroundHit;
        private float   _nextGroundCheckTime;

        // Shared URP shaders and materials.
        private static Shader   _beamShader;
        private static Shader   _groundFlareShader;
        private static Material _sharedBeamMaterial;
        private static Material _sharedHaloMaterial;

        public ItemRarity CurrentRarity       => currentRarity;
        public GameObject VfxRoot             => _vfxRoot;
        public GameObject BeamObject          => _beamObject;
        public GameObject HaloObject          => _haloObject;
        public Light      PointLight          => _pointLight;
        public IReadOnlyList<GameObject> OverlayObjects => _overlayObjects;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _pulseOffset = Random.Range(0f, 100f);
            _spawnTime = Time.time;
            _beamYaw = Random.Range(0f, 360f);
            _haloYaw = Random.Range(0f, 360f);

            ConfigureDropPhysics();
            EnsureVfxRoot();
        }

        private void Start()
        {
            if (!_isSetup)
            {
                Setup(currentRarity);
            }
            CheckGround();
        }

        private void OnEnable()
        {
            if (_vfxRoot != null)
                _vfxRoot.SetActive(true);
        }

        private void OnDisable()
        {
            if (_vfxRoot != null)
                _vfxRoot.SetActive(false);
        }

        private static int _vfxCounter = 0;

        private void EnsureVfxRoot()
        {
            if (_vfxRoot == null)
            {
                // Isolated global root in world space with guaranteed scale (1,1,1).
                _vfxRoot = new GameObject($"LootVFX_{name}_{++_vfxCounter}");
                _vfxRoot.transform.SetParent(null, false);
                _vfxRoot.transform.position = transform.position;
                _vfxRoot.transform.rotation = Quaternion.identity;
                _vfxRoot.transform.localScale = Vector3.one;
            }
        }

        public void Setup(ItemRarity rarity)
        {
            // SyncVar/client initialization may repeat Setup while a drop is moving.
            // Rebuilding the same rarity must not release an already caught item.
            if (_isSetup && currentRarity == rarity) return;
            if (!_isCollecting) ConfigureDropPhysics();
            _arrivalTime = float.NegativeInfinity;
            _hoverVelocity = Vector3.zero;
            _isSetup       = true;
            currentRarity  = rarity;
            _baseIntensity = ItemTierHelper.GetLightIntensity(rarity);
            _baseRange     = ItemTierHelper.GetLightRange(rarity);

            Color tierColor = ItemTierHelper.GetColor(rarity);

            // 1. White items (Common): NO visual effect.
            if (rarity == ItemRarity.Common)
            {
                CleanupLight();
                CleanupBeam();
                CleanupHalo();
                CleanupParticles();
                ClearWaveOverlays();
                if (_vfxRoot != null) _vfxRoot.SetActive(false);

                _isSettled = false;
                if (_rb != null && !_isCollecting)
                {
                    _rb.isKinematic = false;
                    _rb.useGravity = true;
                }
                return;
            }

            // 2. Green and blue items (Uncommon and Rare): only the outline and animated wave gradient shader (no sky beam).
            if (rarity == ItemRarity.Uncommon || rarity == ItemRarity.Rare)
            {
                CleanupLight();
                CleanupBeam();
                CleanupHalo();
                CleanupParticles();
                if (_vfxRoot != null) _vfxRoot.SetActive(false);
                SetupWaveOverlay(rarity);

                _isSettled = false;
                if (_rb != null && !_isCollecting)
                {
                    _rb.isKinematic = false;
                    _rb.useGravity = true;
                }
                return;
            }

            // 3. Epic and above (Epic, Legendary, Cursed):
            // Retain the skyward vertical beam, ground halo, point light, and particles,
            // and ALSO receive the outline + wave shader in their respective colors!
            EnsureVfxRoot();
            if (_vfxRoot != null) _vfxRoot.SetActive(true);
            EnsureSharedMaterials();
            SetupPointLight(tierColor);
            SetupVerticalBeam(rarity, tierColor);
            SetupGroundHalo(rarity, tierColor);
            SetupParticles(rarity, tierColor);
            SetupWaveOverlay(rarity);

            _isSettled = false;
            if (_rb != null && !_isCollecting)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
            }
        }

        public void SetupGold()
        {
            // Keep coin physics free while applying the existing wave/outline in gold.
            Setup(ItemRarity.Common);
            SetupWaveOverlay(ItemRarity.Legendary);
        }

        private void CleanupLight()
        {
            if (_pointLight != null)
            {
                DestroyVisual(_pointLight.gameObject);
                _pointLight = null;
            }
        }

        private void CleanupBeam()
        {
            if (_beamObject != null)
            {
                DestroyVisual(_beamObject);
                _beamObject = null;
                _beamTransform = null;
            }
            if (_beamMesh != null)
            {
                DestroyVisual(_beamMesh);
                _beamMesh = null;
            }
        }

        private void CleanupHalo()
        {
            if (_haloObject != null)
            {
                DestroyVisual(_haloObject);
                _haloObject = null;
                _haloTransform = null;
            }
            if (_haloMesh != null)
            {
                DestroyVisual(_haloMesh);
                _haloMesh = null;
            }
        }

        private void CleanupParticles()
        {
            if (_particles != null)
            {
                DestroyVisual(_particles.gameObject);
                _particles = null;
            }
        }

        private static Material GetSharedWaveMaterial(ItemRarity rarity)
        {
            if (_waveOverlayShader == null)
            {
                _waveOverlayShader = Shader.Find("Duskborn/Loot/ItemRarityWaveOverlay")
                                  ?? Shader.Find("Universal Render Pipeline/Lit")
                                  ?? Shader.Find("Sprites/Default");
            }
            if (_waveOverlayShader == null) return null;

            _sharedWaveMaterials ??= new Dictionary<ItemRarity, Material>();
            if (!_sharedWaveMaterials.TryGetValue(rarity, out var mat) || mat == null)
            {
                mat = new Material(_waveOverlayShader) { name = $"M_ItemRarityWaveOverlay_{rarity}" };
                Color col = ItemTierHelper.GetColor(rarity);
                mat.SetColor("_Color", col);
                mat.SetColor("_OutlineColor", col);
                _sharedWaveMaterials[rarity] = mat;
            }
            return mat;
        }

        private void SetupWaveOverlay(ItemRarity rarity)
        {
            ClearWaveOverlays();

            if (rarity < ItemRarity.Uncommon)
            {
                return;
            }

            var mat = GetSharedWaveMaterial(rarity);
            if (mat == null) return;

            var meshFilters = GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (_vfxRoot != null && (mf.transform == _vfxRoot.transform || mf.transform.IsChildOf(_vfxRoot.transform))) continue;
                if (mf.gameObject.name.Contains("Diablo") || mf.gameObject.name.Contains("Halo") || mf.gameObject.name.Contains("Overlay")) continue;

                var overlayObj = new GameObject("RarityWaveOverlay");
                overlayObj.transform.SetParent(mf.transform, false);
                overlayObj.transform.localPosition = Vector3.zero;
                overlayObj.transform.localRotation = Quaternion.identity;
                overlayObj.transform.localScale = Vector3.one;

                var overlayMf = overlayObj.AddComponent<MeshFilter>();
                overlayMf.sharedMesh = mf.sharedMesh;

                var overlayMr = overlayObj.AddComponent<MeshRenderer>();
                overlayMr.sharedMaterial = mat;
                overlayMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlayMr.receiveShadows = false;

                _overlayObjects.Add(overlayObj);
            }
        }

        private void ClearWaveOverlays()
        {
            for (int i = 0; i < _overlayObjects.Count; i++)
            {
                if (_overlayObjects[i] != null)
                    DestroyVisual(_overlayObjects[i]);
            }
            _overlayObjects.Clear();
        }

        private static void EnsureSharedMaterials()
        {
            if (_beamShader == null)
            {
                _beamShader = Shader.Find("Duskborn/VFX/DiabloLootBeam")
                           ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Particles/Standard Unlit")
                           ?? Shader.Find("Sprites/Default");
            }

            if (_groundFlareShader == null)
            {
                _groundFlareShader = Shader.Find("Duskborn/VFX/DiabloGroundFlare")
                                  ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                  ?? Shader.Find("Particles/Standard Unlit")
                                  ?? Shader.Find("Sprites/Default");
            }

            if (_sharedBeamMaterial == null && _beamShader != null)
            {
                _sharedBeamMaterial = new Material(_beamShader) { name = "M_DiabloLootBeam_Shared" };
                ConfigureAdditiveProps(_sharedBeamMaterial);
            }

            if (_sharedHaloMaterial == null && _groundFlareShader != null)
            {
                _sharedHaloMaterial = new Material(_groundFlareShader) { name = "M_DiabloGroundFlare_Shared" };
                ConfigureAdditiveProps(_sharedHaloMaterial);
            }
        }

        private static void ConfigureAdditiveProps(Material mat)
        {
            if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend", 1f);
            if (mat.HasProperty("_Cull"))      mat.SetFloat("_Cull", 0f);
            if (mat.HasProperty("_ZWrite"))    mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = 3100;
        }

        private void SetupPointLight(Color tierColor)
        {
            if (_pointLight == null)
            {
                var lightObj = new GameObject("DropPointLight");
                lightObj.transform.SetParent(_vfxRoot.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                _pointLight = lightObj.AddComponent<Light>();
            }

            _pointLight.type = LightType.Point;
            _pointLight.color = tierColor;
            _pointLight.range = _baseRange;
            _pointLight.intensity = _baseIntensity;
            _pointLight.shadows = LightShadows.None;
        }

        private void SetupVerticalBeam(ItemRarity rarity, Color tierColor)
        {
            if (_beamObject == null)
            {
                _beamObject = new GameObject("DiabloLootBeam");
                _beamObject.transform.SetParent(_vfxRoot.transform, false);
                _beamObject.transform.localPosition = Vector3.zero;
                _beamTransform = _beamObject.transform;

                var mf = _beamObject.AddComponent<MeshFilter>();
                var mr = _beamObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _sharedBeamMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _beamMesh = CreateBeamMesh(rarity, tierColor);
                mf.sharedMesh = _beamMesh;
            }
            else
            {
                var mf = _beamObject.GetComponent<MeshFilter>();
                if (_beamMesh != null) DestroyVisual(_beamMesh);
                _beamMesh = CreateBeamMesh(rarity, tierColor);
                mf.sharedMesh = _beamMesh;
            }
        }

        private Mesh CreateBeamMesh(ItemRarity rarity, Color tierColor)
        {
            var mesh = new Mesh { name = $"LootBeamMesh_{rarity}" };

            float h = ItemTierHelper.GetBeamHeight(rarity);
            float w = ItemTierHelper.GetBeamWidth(rarity) * 0.5f;
            float alpha = ItemTierHelper.GetBeamAlpha(rarity);

            Color col = new Color(tierColor.r, tierColor.g, tierColor.b, alpha);

            // 3 vertical planes crossed at 60° (six-point star cylinder with 360° volume).
            float[] angles = new float[] { 0f, 60f, 120f };
            int planeCount = angles.Length;

            Vector3[] vertices = new Vector3[planeCount * 4];
            Color[] colors = new Color[planeCount * 4];
            Vector2[] uvs = new Vector2[planeCount * 4];
            int[] tris = new int[planeCount * 12];

            for (int i = 0; i < planeCount; i++)
            {
                float rad = angles[i] * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                int vBase = i * 4;
                // Base
                vertices[vBase + 0] = -dir * w;
                vertices[vBase + 1] =  dir * w;
                // Top (tapers smoothly toward the sky).
                vertices[vBase + 2] = -dir * (w * 0.70f) + Vector3.up * h;
                vertices[vBase + 3] =  dir * (w * 0.70f) + Vector3.up * h;

                colors[vBase + 0] = col;
                colors[vBase + 1] = col;
                colors[vBase + 2] = col;
                colors[vBase + 3] = col;

                uvs[vBase + 0] = new Vector2(0f, 0f);
                uvs[vBase + 1] = new Vector2(1f, 0f);
                uvs[vBase + 2] = new Vector2(0f, 1f);
                uvs[vBase + 3] = new Vector2(1f, 1f);

                int tBase = i * 12;
                // Front face
                tris[tBase + 0] = vBase + 0;
                tris[tBase + 1] = vBase + 2;
                tris[tBase + 2] = vBase + 1;

                tris[tBase + 3] = vBase + 1;
                tris[tBase + 4] = vBase + 2;
                tris[tBase + 5] = vBase + 3;

                // Back face
                tris[tBase + 6] = vBase + 1;
                tris[tBase + 7] = vBase + 2;
                tris[tBase + 8] = vBase + 0;

                tris[tBase + 9]  = vBase + 3;
                tris[tBase + 10] = vBase + 2;
                tris[tBase + 11] = vBase + 1;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            return mesh;
        }

        private void SetupGroundHalo(ItemRarity rarity, Color tierColor)
        {
            if (_haloObject == null)
            {
                _haloObject = new GameObject("GroundHalo");
                _haloObject.transform.SetParent(_vfxRoot.transform, false);
                _haloObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                _haloTransform = _haloObject.transform;

                var mf = _haloObject.AddComponent<MeshFilter>();
                var mr = _haloObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _sharedHaloMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _haloMesh = CreateHaloMesh(rarity, tierColor);
                mf.sharedMesh = _haloMesh;
            }
            else
            {
                var mf = _haloObject.GetComponent<MeshFilter>();
                if (_haloMesh != null) DestroyVisual(_haloMesh);
                _haloMesh = CreateHaloMesh(rarity, tierColor);
                mf.sharedMesh = _haloMesh;
            }
        }

        private Mesh CreateHaloMesh(ItemRarity rarity, Color tierColor)
        {
            var mesh = new Mesh { name = $"GroundHaloMesh_{rarity}" };

            float r = ItemTierHelper.GetHaloScale(rarity) * 0.5f;
            Color col = new Color(tierColor.r, tierColor.g, tierColor.b, 1.0f);

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-r, 0f, -r),
                new Vector3( r, 0f, -r),
                new Vector3(-r, 0f,  r),
                new Vector3( r, 0f,  r)
            };

            Color[] colors = new Color[4] { col, col, col, col };
            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            int[] tris = new int[12]
            {
                0, 2, 1,
                1, 2, 3,
                1, 2, 0,
                3, 2, 1
            };

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            return mesh;
        }

        private void SetupParticles(ItemRarity rarity, Color tierColor)
        {
            if (rarity < ItemRarity.Rare)
            {
                if (_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            if (_particles == null)
            {
                var pgo = new GameObject("TierSparkles");
                pgo.transform.SetParent(_vfxRoot.transform, false);
                pgo.transform.localPosition = new Vector3(0f, 0.15f, 0f);

                _particles = pgo.AddComponent<ParticleSystem>();
                var pRenderer = pgo.GetComponent<ParticleSystemRenderer>();
                pRenderer.sharedMaterial = _sharedHaloMaterial;

                var main = _particles.main;
                main.playOnAwake = true;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = 18;
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);

                var emission = _particles.emission;
                emission.rateOverTime = rarity == ItemRarity.Legendary ? 8f : (rarity == ItemRarity.Epic ? 5f : 3f);

                var shape = _particles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.16f;
                shape.rotation = new Vector3(90f, 0f, 0f);

                var vel = _particles.velocityOverLifetime;
                vel.enabled = true;
                vel.y = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
                vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

                var col = _particles.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new(Color.white, 0f), new(tierColor, 0.35f), new(tierColor, 1f) },
                    new GradientAlphaKey[] { new(0.9f, 0f), new(0.8f, 0.5f), new(0f, 1f) }
                );
                col.color = grad;
            }
            else
            {
                var main = _particles.main;
                main.startColor = tierColor;
                var emission = _particles.emission;
                emission.rateOverTime = rarity == ItemRarity.Legendary ? 8f : (rarity == ItemRarity.Epic ? 5f : 3f);
            }
        }

        public Vector3 GetVisualCenter()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (r.name.Contains("Diablo") || r.name.Contains("Halo") || r.name.Contains("Overlay") || r.name.Contains("VFX") || r.name.Contains("DropPointLight")) continue;
                if (_vfxRoot != null && r.transform.IsChildOf(_vfxRoot.transform)) continue;
                return r.bounds.center;
            }

            var col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                return col.bounds.center;
            }
            return transform.position;
        }

        private void CheckGround()
        {
            int layerMask = ~LayerMask.GetMask("Resource", "Ignore Raycast");
            if (Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 60f, layerMask, QueryTriggerInteraction.Ignore))
            {
                _groundNormal = hit.normal;
                _groundPoint  = hit.point;
                _hasGroundHit = true;
            }
            else
            {
                _groundNormal = Vector3.up;
                _groundPoint  = transform.position;
                _hasGroundHit = false;
            }
        }

        private void Update()
        {
            float time = Time.time;
            UpdateIdleHover(time);
        }

        private void LateUpdate()
        {
            if (_vfxRoot == null || !_vfxRoot.activeSelf) return;

            float time = Time.time;
            _beamYaw = (_beamYaw + 18f * Time.deltaTime) % 360f;
            _haloYaw = (_haloYaw - 12f * Time.deltaTime) % 360f;

            Vector3 visualCenter = GetVisualCenter();

            // Isolated global root synchronized with the item's geometric center.
            _vfxRoot.transform.position = visualCenter;
            _vfxRoot.transform.rotation = Quaternion.identity;
            _vfxRoot.transform.localScale = Vector3.one;

            // Arrival accent starts at contact/hover catch, rather than expiring in flight.
            float elapsedSinceDrop = time - _arrivalTime;
            float dropSpike = 1f;
            if (elapsedSinceDrop < 0.40f)
            {
                float t = elapsedSinceDrop / 0.40f;
                dropSpike = Mathf.Lerp(1.6f, 1.0f, t * t);
            }

            if (!_isSettled && time >= _nextGroundCheckTime)
            {
                CheckGround();
                _nextGroundCheckTime = time + 0.15f;
            }

            // THE BEAM ALWAYS EXTENDS FROM THE OBJECT'S VISUAL CENTER TOWARD THE SKY (Vector3.up),
            // perfectly aligned in X, Y, and Z with the 3D model center.
            if (_beamTransform != null)
            {
                _beamTransform.position = visualCenter;
                _beamTransform.rotation = Quaternion.Euler(0f, _beamYaw, 0f);

                float beamPulse = (1f + 0.04f * Mathf.Sin(time * 2.8f + _pulseOffset)) * dropSpike;
                _beamTransform.localScale = new Vector3(beamPulse, 1f, beamPulse);
            }

            // THE HALO RESTS FLAT ON THE TERRAIN directly beneath the object center.
            if (_haloTransform != null)
            {
                Vector3 haloPos = _hasGroundHit
                    ? new Vector3(visualCenter.x, _groundPoint.y + _groundNormal.y * 0.015f, visualCenter.z)
                    : new Vector3(visualCenter.x, transform.position.y + 0.02f, visualCenter.z);
                _haloTransform.position = haloPos;
                Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, _groundNormal);
                _haloTransform.rotation = slopeRot * Quaternion.Euler(0f, _haloYaw, 0f);

                float haloPulse = (1f + 0.06f * Mathf.Sin(time * 2.2f + _pulseOffset)) * dropSpike;
                _haloTransform.localScale = new Vector3(haloPulse, 1f, haloPulse);
            }

            // Soft light ("dim light") positioned at the object center.
            if (_pointLight != null)
            {
                _pointLight.transform.position = visualCenter;
                float pulse = 1f + 0.12f * Mathf.Sin(time * 2.6f + _pulseOffset);
                _pointLight.intensity = _baseIntensity * pulse * dropSpike;
            }
        }

        private void ConfigureDropPhysics()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb == null) return;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.linearDamping = AirDrag;
            _rb.angularDamping = 0.35f;

            if (_dropContactMaterial == null)
            {
                _dropContactMaterial = new PhysicsMaterial("Loot weighted contact")
                {
                    staticFriction = 0.65f,
                    dynamicFriction = 0.5f,
                    bounciness = 0.22f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (!collider.isTrigger && collider.attachedRigidbody == _rb)
                    collider.sharedMaterial = _dropContactMaterial;
            }
        }

        public void BeginDropMotion(Vector3 velocity, Vector3 angularVelocity)
        {
            _isCollecting = false;
            _spawnTime = Time.time;
            _arrivalTime = float.NegativeInfinity;
            _lastGroundContact = float.NegativeInfinity;
            _isSettled = false;
            _hoverVelocity = Vector3.zero;
            ConfigureDropPhysics();
            if (_rb == null) return;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = velocity;
            // Direct angular speed avoids prefab inertia/scale changing the tumble.
            _rb.angularVelocity = angularVelocity;
            _rb.WakeUp();
        }

        public void BeginCollectionMotion()
        {
            _isCollecting = true;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb == null) return;
            // Physics interpolation must not overwrite the per-render-frame magnet pose.
            Vector3 visiblePosition = transform.position;
            Quaternion visibleRotation = transform.rotation;
            _rb.interpolation = RigidbodyInterpolation.None;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.position = visiblePosition;
            _rb.rotation = visibleRotation;
            transform.SetPositionAndRotation(visiblePosition, visibleRotation);
        }

        private void FixedUpdate()
        {
            // Magnet collection owns a kinematic body; never fight that movement.
            if (_rb == null || _rb.isKinematic || _rb.IsSleeping()) return;
            bool touchingGround = Time.fixedTime - _lastGroundContact <= Time.fixedDeltaTime * 1.5f;
            _rb.linearDamping = touchingGround ? 5f : AirDrag;
            _rb.angularDamping = touchingGround ? 8f : 0.35f;
            if (_rb.useGravity)
                _rb.AddForce(Physics.gravity * (GravityScale - 1f), ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision) => RegisterGroundContact(collision);
        private void OnCollisionStay(Collision collision) => RegisterGroundContact(collision);

        private void RegisterGroundContact(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) < 0.5f) continue;
                _lastGroundContact = Time.fixedTime;
                if (float.IsNegativeInfinity(_arrivalTime)) _arrivalTime = Time.time;
                break;
            }
        }

        // Exact damped spring step: preserves incoming momentum, allows one small
        // overshoot, and converges identically at different rendering frame rates.
        public static void StepHoverSpring(ref Vector3 position, ref Vector3 velocity,
            Vector3 target, float deltaTime)
        {
            const float damping = 9f;
            const float frequency = 11f;
            float decay = Mathf.Exp(-damping * deltaTime);
            float cos = Mathf.Cos(frequency * deltaTime);
            float sin = Mathf.Sin(frequency * deltaTime);
            Vector3 offset = position - target;
            Vector3 wave = (velocity + damping * offset) / frequency;
            position = target + decay * (offset * cos + wave * sin);
            velocity = decay * (velocity * cos - (damping * wave + frequency * offset) * sin);
        }

        private void UpdateIdleHover(float time)
        {
            if (_isCollecting) return;
            if (!ItemTierHelper.ShouldFloatInAir(currentRarity)) return;

            if (!_isSettled && time >= _nextGroundCheckTime)
            {
                CheckGround();
                _nextGroundCheckTime = time + 0.08f;
            }
            if (!_hasGroundHit) return; // No timeout freeze over cliffs/missing ground.
            float targetHoverY = _groundPoint.y + ItemTierHelper.GetHoverHeight(currentRarity);

            if (!_isSettled)
            {
                // Let the launch/apex read before catching the descending item.
                if (time - _spawnTime < 0.06f) return;
                if (_rb != null && (_rb.linearVelocity.y > 0f ||
                    transform.position.y > targetHoverY + 0.12f)) return;

                _hoverVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
                _settledPosition = new Vector3(transform.position.x, targetHoverY, transform.position.z);
                _catchRotation = transform.rotation;
                _hoverRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                _arrivalTime = time;
                _isSettled = true;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.isKinematic = true;
                    _rb.useGravity = false;
                }
            }

            float age = time - _arrivalTime;
            float idleBlend = 1f - Mathf.Exp(-age * 4f);
            Vector3 target = _settledPosition;
            target.y += Mathf.Sin(age * 2.2f) * 0.04f * idleBlend;
            Vector3 position = transform.position;
            StepHoverSpring(ref position, ref _hoverVelocity, target, Time.deltaTime);
            transform.position = position;
            // Recover gracefully from the launch tumble, then ease into idle spin.
            _hoverRotation = Quaternion.AngleAxis(38f * idleBlend * Time.deltaTime, Vector3.up) * _hoverRotation;
            transform.rotation = Quaternion.Slerp(_catchRotation, _hoverRotation, 1f - Mathf.Exp(-age * 7f));
        }

        private static void DestroyVisual(UnityEngine.Object visual)
        {
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
        }

        private void OnDestroy()
        {
            CleanupLight();
            CleanupBeam();
            CleanupHalo();
            CleanupParticles();
            ClearWaveOverlays();
            if (_vfxRoot != null) DestroyVisual(_vfxRoot);
        }
    }
}
