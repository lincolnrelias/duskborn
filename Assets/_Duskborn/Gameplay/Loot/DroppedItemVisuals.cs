using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    /// <summary>
    /// Efeito visual completo estilo Diablo para itens caídos no chão:
    /// - Camada 1: Ponto de contato no solo com hotspot brilhante, ondulações concêntricas e pool difuso.
    /// - Camada 2: Coluna de plasma vertical em ascensão contínua (UV shader animado, núcleo incandescente branco, queda Gaussiana).
    /// - Camada 3: Motes/centelhas que sobem pelo feixe em direção aos céus (Raro, Épico, Lendário).
    /// - Camada 4: Flash/spike inicial no impacto que assenta na coluna estável.
    /// - Camada 5: Luz pontual suave ("dim light") com pulsação orgânica.
    /// 
    /// IMPORTANTE: Todo o conjunto visual é isolado em um GameObject raiz de mundo independente (_vfxRoot)
    /// com escala fixa (1, 1, 1), tornando-o 100% imune a prefabs com escalas arbitrárias
    /// como ferro (20x20x20) e madeira (15x30x30).
    /// </summary>
    [DisallowMultipleComponent]
    public class DroppedItemVisuals : MonoBehaviour
    {
        [Header("Configuração de Tier")]
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

        private float _baseIntensity;
        private float _baseRange;
        private float _pulseOffset;
        private float _beamYaw;
        private float _haloYaw;
        private bool  _isSettled;
        private Vector3 _settledPosition;
        private float _spawnTime;

        // Ancoragem ao terreno
        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _groundPoint;
        private bool    _hasGroundHit;
        private float   _nextGroundCheckTime;

        // Shaders e materiais URP compartilhados
        private static Shader   _beamShader;
        private static Shader   _groundFlareShader;
        private static Material _sharedBeamMaterial;
        private static Material _sharedHaloMaterial;

        public ItemRarity CurrentRarity => currentRarity;
        public GameObject VfxRoot      => _vfxRoot;
        public GameObject BeamObject   => _beamObject;
        public GameObject HaloObject   => _haloObject;
        public Light      PointLight   => _pointLight;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _pulseOffset = Random.Range(0f, 100f);
            _spawnTime = Time.time;
            _beamYaw = Random.Range(0f, 360f);
            _haloYaw = Random.Range(0f, 360f);

            EnsureVfxRoot();
        }

        private void Start()
        {
            if (_pointLight == null)
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
                // Raiz global isolada em world space com escala (1,1,1) garantida
                _vfxRoot = new GameObject($"LootVFX_{name}_{++_vfxCounter}");
                _vfxRoot.transform.SetParent(null, false);
                _vfxRoot.transform.position = transform.position;
                _vfxRoot.transform.rotation = Quaternion.identity;
                _vfxRoot.transform.localScale = Vector3.one;
            }
        }

        public void Setup(ItemRarity rarity)
        {
            currentRarity  = rarity;
            _baseIntensity = ItemTierHelper.GetLightIntensity(rarity);
            _baseRange     = ItemTierHelper.GetLightRange(rarity);

            Color tierColor = ItemTierHelper.GetColor(rarity);

            EnsureVfxRoot();
            EnsureSharedMaterials();
            SetupPointLight(tierColor);
            SetupVerticalBeam(rarity, tierColor);
            SetupGroundHalo(rarity, tierColor);
            SetupParticles(rarity, tierColor);

            if (!ItemTierHelper.ShouldFloatInAir(rarity))
            {
                // Itens Comuns e Incomuns devem cair fisicamente no chão com gravidade como de costume
                _isSettled = false;
                if (_rb != null)
                {
                    _rb.isKinematic = false;
                    _rb.useGravity = true;
                }
            }
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
                if (_beamMesh != null) Destroy(_beamMesh);
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

            // 3 planos verticais cruzados a 60° (cilindro estelar de 6 pontas com volume 360°)
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
                // Topo (afilado suavemente em direção aos céus)
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
                // Face frontal
                tris[tBase + 0] = vBase + 0;
                tris[tBase + 1] = vBase + 2;
                tris[tBase + 2] = vBase + 1;

                tris[tBase + 3] = vBase + 1;
                tris[tBase + 4] = vBase + 2;
                tris[tBase + 5] = vBase + 3;

                // Face traseira
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
                if (_haloMesh != null) Destroy(_haloMesh);
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

        private void CheckGround()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
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
            if (_vfxRoot == null) return;

            float time = Time.time;
            _beamYaw = (_beamYaw + 18f * Time.deltaTime) % 360f;
            _haloYaw = (_haloYaw - 12f * Time.deltaTime) % 360f;

            // Raiz global isolada sincronizada com a posição do item, mantendo escala limpa (1, 1, 1)
            _vfxRoot.transform.position = transform.position;
            _vfxRoot.transform.rotation = Quaternion.identity;
            _vfxRoot.transform.localScale = Vector3.one;

            // Efeito de impacto inicial (flash/spike que assenta suavemente)
            float elapsedSinceDrop = time - _spawnTime;
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

            // O FEIXE SEMPRE APONTA RIGOROSAMENTE PARA O CÉU (Vector3.up),
            // completamente imune a inclinações do Rigidbody ou do terreno.
            if (_beamTransform != null)
            {
                _beamTransform.position = transform.position;
                _beamTransform.rotation = Quaternion.Euler(0f, _beamYaw, 0f);

                float beamPulse = (1f + 0.04f * Mathf.Sin(time * 2.8f + _pulseOffset)) * dropSpike;
                _beamTransform.localScale = new Vector3(beamPulse, 1f, beamPulse);
            }

            // O HALO REPOUSA PLANO NO TERRENO (acompanhando a inclinação do solo)
            if (_haloTransform != null)
            {
                Vector3 haloPos = _hasGroundHit ? _groundPoint + _groundNormal * 0.015f : transform.position + Vector3.up * 0.02f;
                _haloTransform.position = haloPos;
                Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, _groundNormal);
                _haloTransform.rotation = slopeRot * Quaternion.Euler(0f, _haloYaw, 0f);

                float haloPulse = (1f + 0.06f * Mathf.Sin(time * 2.2f + _pulseOffset)) * dropSpike;
                _haloTransform.localScale = new Vector3(haloPulse, 1f, haloPulse);
            }

            // Luz suave ("dim light")
            if (_pointLight != null)
            {
                _pointLight.transform.position = transform.position + Vector3.up * 0.15f;
                float pulse = 1f + 0.12f * Mathf.Sin(time * 2.6f + _pulseOffset);
                _pointLight.intensity = _baseIntensity * pulse * dropSpike;
            }
        }

        private void UpdateIdleHover(float time)
        {
            // Apenas itens de raridade Épica ou superior (Épico, Lendário) ficam flutuando no ar.
            // Os demais (Comum, Incomum e Raro) caem no chão normalmente sob física e gravidade.
            if (!ItemTierHelper.ShouldFloatInAir(currentRarity))
            {
                return;
            }

            if (!_isSettled)
            {
                if (_rb != null)
                {
                    bool isSlow = _rb.linearVelocity.sqrMagnitude < 0.04f;
                    bool timeout = (time - _spawnTime) > 0.85f;

                    if (isSlow || timeout)
                    {
                        _isSettled = true;
                        _settledPosition = transform.position;
                        _rb.isKinematic = true;
                        CheckGround();
                    }
                }
                else
                {
                    _isSettled = true;
                    _settledPosition = transform.position;
                    CheckGround();
                }
            }

            if (_isSettled)
            {
                // Levitação suave senoidal com leve rotação contínua do modelo do item
                float bobY = Mathf.Sin((time + _pulseOffset) * 2.2f) * 0.035f;
                transform.position = _settledPosition + Vector3.up * bobY;
                transform.Rotate(Vector3.up, 28f * Time.deltaTime, Space.World);
            }
        }

        private void OnDestroy()
        {
            if (_vfxRoot != null) Destroy(_vfxRoot);
            if (_beamMesh != null) Destroy(_beamMesh);
            if (_haloMesh != null) Destroy(_haloMesh);
        }
    }
}
