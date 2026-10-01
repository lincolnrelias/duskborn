#if UNITY_EDITOR
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;
using Duskborn.Gameplay.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public sealed partial class ItemFittingStudioWindow
    {
        private GameObject _projectileRoot;
        private ProjectileDefinition _previewProjectile;
        private GameObject _previewProjectilePrefab;
        private bool _showProjectile = true;
        private bool _playProjectile;
        private bool _followProjectile;
        private float _projectileTime;
        private bool _projectileUniformScale = true;

        private ProjectileDefinition SelectedProjectile =>
            (selectedWeapon?.Behaviour is RangedWeaponBehaviour ranged) ? ranged.projectile : null;

        private GameObject SpawnSettingsPrefab => customCharacterPrefab != null
            ? customCharacterPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterPath);

        internal static void GetPreviewSpawn(GameObject source, GameObject instance, RangedWeaponBehaviour ranged,
            out Vector3 position, out Quaternion rotation)
        {
            var combat = source != null ? source.GetComponentInChildren<PlayerCombat>(true) : null;
            var basis = instance.transform;
            Transform socket = null;
            if (combat != null)
            {
                string path = AnimationUtility.CalculateTransformPath(combat.transform, source.transform);
                basis = string.IsNullOrEmpty(path) ? instance.transform : instance.transform.Find(path);
                if (basis == null) basis = instance.transform;
                if (combat.RangedSpawnPoint != null)
                {
                    path = AnimationUtility.CalculateTransformPath(combat.RangedSpawnPoint, source.transform);
                    socket = string.IsNullOrEmpty(path) ? instance.transform : instance.transform.Find(path);
                }
            }
            // Read current asset values, not the stale settings on the preview clone.
            PlayerCombat.ResolveRangedSpawn(basis, socket,
                combat != null ? combat.RangedSpawnOffset : Vector3.up * 1.3f,
                combat != null ? combat.RangedSpawnRotationOffset : Vector3.zero,
                ranged, out position, out rotation);
        }

        private void DrawProjectileSection()
        {
            if (!(selectedWeapon?.Behaviour is RangedWeaponBehaviour ranged)) return;

            EditorGUILayout.LabelField("Projétil da Arma", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("Edita os assets usados no disparo real. Alterações afetam todas as armas que compartilham este comportamento ou projétil. Use Ctrl+Z para desfazer.", MessageType.Info);

            bool shouldRebuild = false;
            bool shouldRepose = false;

            using (var behaviour = new SerializedObject(ranged))
            {
                behaviour.Update();
                var projProp = behaviour.FindProperty("projectile");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(projProp, new GUIContent("Projétil"));
                if (EditorGUI.EndChangeCheck())
                {
                    shouldRebuild = true;
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Origem do Disparo (Spawn Offset)", EditorStyles.boldLabel);
                var customOffsetProp = behaviour.FindProperty("useCustomSpawnOffset");
                var posOffsetProp = behaviour.FindProperty("spawnPositionOffset");
                var rotOffsetProp = behaviour.FindProperty("spawnRotationOffset");

                EditorGUILayout.PropertyField(customOffsetProp, new GUIContent("Override nesta Arma", "Se marcado, esta arma usa seus próprios offsets em vez do padrão global do jogador."));

                if (customOffsetProp.boolValue)
                {
                    EditorGUILayout.PropertyField(posOffsetProp, new GUIContent("Posição Relativa", "Posição (X = Direita, Y = Cima, Z = Frente) relativa ao jogador."));
                    EditorGUILayout.PropertyField(rotOffsetProp, new GUIContent("Rotação Relativa", "Ângulos de Euler (Pitch, Yaw, Roll) relativos à direção do jogador/mira."));

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Alinhar com a Arma", EditorStyles.miniButton))
                    {
                        if (_weaponInstance != null)
                        {
                            var combat = _characterInstance.GetComponentInChildren<PlayerCombat>(true);
                            var basis = combat != null ? combat.transform : _characterInstance.transform;
                            Vector3 weaponLocal = basis != null
                                ? basis.InverseTransformPoint(_weaponInstance.transform.position)
                                : _weaponInstance.transform.position;
                            posOffsetProp.vector3Value = weaponLocal;
                            shouldRepose = true;
                        }
                    }
                    if (GUILayout.Button("Resetar (0, 1.3, 0)", EditorStyles.miniButton))
                    {
                        posOffsetProp.vector3Value = new Vector3(0f, 1.3f, 0f);
                        rotOffsetProp.vector3Value = Vector3.zero;
                        shouldRepose = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("Usando offset padrão global do jogador. Você pode editá-lo abaixo ou marcar 'Override nesta Arma'.", MessageType.None);

                    var playerPrefab = SpawnSettingsPrefab;
                    var defaultCombat = playerPrefab != null ? playerPrefab.GetComponentInChildren<PlayerCombat>(true) : null;

                    if (defaultCombat != null)
                    {
                        var targetObj = defaultCombat;
                        using (var playerSo = new SerializedObject(targetObj))
                        {
                            playerSo.Update();
                            var defPos = playerSo.FindProperty("rangedSpawnOffset");
                            var defRot = playerSo.FindProperty("rangedSpawnRotationOffset");
                            if (defPos != null && defRot != null)
                            {
                                using (new EditorGUI.DisabledScope(defaultCombat.RangedSpawnPoint != null))
                                    EditorGUILayout.PropertyField(defPos, new GUIContent("Posição Padrão (Player)", "Offset padrão de spawn no PlayerCombat."));
                                if (defaultCombat.RangedSpawnPoint != null)
                                    EditorGUILayout.HelpBox("O jogador usa Ranged Spawn Point. Para substituir essa origem, marque Override nesta Arma.", MessageType.Info);
                                EditorGUILayout.PropertyField(defRot, new GUIContent("Rotação Padrão (Player)", "Offset padrão de rotação no PlayerCombat."));
                                if (playerSo.ApplyModifiedProperties())
                                {
                                    EditorUtility.SetDirty(targetObj);
                                    shouldRepose = true;
                                }
                            }
                        }
                    }
                }

                if (behaviour.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(ranged);
                    shouldRepose = true;
                }
            }

            bool spawnEdited = shouldRepose;
            var projectile = ranged.projectile;
            if (projectile == null)
            {
                EditorGUILayout.HelpBox("Atribua ou crie um projétil para esta arma.", MessageType.Warning);
                if (GUILayout.Button("Criar Projétil..."))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Criar Projétil", "NovoProjetil", "asset", "Escolha onde salvar o projétil.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        projectile = CreateInstance<ProjectileDefinition>();
                        AssetDatabase.CreateAsset(projectile, path);
                        Undo.RecordObject(ranged, "Atribuir Projétil");
                        ranged.projectile = projectile;
                        EditorUtility.SetDirty(ranged);
                        shouldRebuild = true;
                    }
                }
            }
            else
            {
                using (var definition = new SerializedObject(projectile))
                {
                    definition.Update();
                    var property = definition.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.name == "m_Script") continue;

                        if (property.name == "visualScale")
                        {
                            EditorGUILayout.PropertyField(property, new GUIContent("Escala Visual", "Multiplicador de escala relativo à escala original do prefab."), true);
                            EditorGUILayout.BeginHorizontal();
                            _projectileUniformScale = EditorGUILayout.ToggleLeft("Uniforme", _projectileUniformScale, GUILayout.Width(75));
                            if (_projectileUniformScale)
                            {
                                float cur = property.vector3Value.x;
                                float uScale = EditorGUILayout.FloatField(cur);
                                if (Mathf.Abs(uScale - cur) > 0.001f)
                                {
                                    property.vector3Value = Vector3.one * Mathf.Max(0.001f, uScale);
                                }
                            }
                            if (GUILayout.Button("Resetar (1, 1, 1)", EditorStyles.miniButton, GUILayout.Width(110)))
                            {
                                property.vector3Value = Vector3.one;
                            }
                            EditorGUILayout.EndHorizontal();
                            continue;
                        }

                        EditorGUILayout.PropertyField(property, true);
                    }
                    if (definition.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(projectile);
                        if (_projectileRoot != null && _projectileRoot.transform.childCount > 0)
                        {
                            projectile.ApplyVisualScale(_projectileRoot.transform.GetChild(0));
                            Repaint();
                        }
                        else
                        {
                            shouldRebuild = true;
                        }
                    }
                }

                if (projectile.visualPrefab == null)
                    EditorGUILayout.HelpBox("Defina Visual Prefab para visualizar e disparar o projétil.", MessageType.Warning);

                EditorGUILayout.HelpBox("Preview de voo livre: usa a origem e rotação de spawn configuradas acima, velocidade, gravidade e duração do projétil.", MessageType.None);
                _showProjectile = EditorGUILayout.Toggle("Mostrar Projétil", _showProjectile);
                _followProjectile = EditorGUILayout.Toggle("Seguir Projétil", _followProjectile);
                using (new EditorGUI.DisabledScope(projectile.visualPrefab == null))
                {
                    EditorGUI.BeginChangeCheck();
                    _projectileTime = EditorGUILayout.Slider("Tempo de Voo (s)", _projectileTime, 0f, Mathf.Max(0.1f, projectile.lifetime));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _playProjectile = false;
                        shouldRepose = true;
                    }
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(_playProjectile ? "Pausar Voo" : "Reproduzir Voo"))
                    {
                        if (_projectileTime >= projectile.lifetime) _projectileTime = 0f;
                        _playProjectile = !_playProjectile;
                        _showProjectile = true;
                        if (_playProjectile) _followProjectile = true;
                    }
                    if (GUILayout.Button("Reiniciar"))
                    {
                        _projectileTime = 0f;
                        _playProjectile = false;
                        shouldRepose = true;
                    }
                    if (GUILayout.Button("Focar Projétil"))
                    {
                        _showProjectile = true;
                        EnsureProjectilePreview();
                        ApplyProjectilePreviewPose();
                        FocusOnProjectile();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("Salvar Configuração do Projétil"))
            {
                AssetDatabase.SaveAssetIfDirty(ranged);
                if (ranged.projectile != null) AssetDatabase.SaveAssetIfDirty(ranged.projectile);
                var playerPrefab = SpawnSettingsPrefab;
                if (playerPrefab != null && PrefabUtility.IsPartOfPrefabAsset(playerPrefab))
                    PrefabUtility.SavePrefabAsset(playerPrefab);
            }
            EditorGUILayout.EndVertical();

            if (spawnEdited)
            {
                // Keep the origin visible while fitting; camera follow otherwise hides translations.
                _projectileTime = 0f;
                _playProjectile = false;
                _followProjectile = false;
            }
            if (shouldRebuild) RebuildProjectilePreview();
            else if (shouldRepose)
            {
                ApplyProjectilePreviewPose();
                Repaint();
            }
        }

        private void OnProjectileUndoRedo()
        {
            RebuildProjectilePreview();
            Repaint();
        }

        private void CleanupProjectilePreview()
        {
            if (_projectileRoot != null) DestroyImmediate(_projectileRoot);
            _projectileRoot = null;
            _previewProjectile = null;
            _previewProjectilePrefab = null;
            _playProjectile = false;
            _projectileTime = 0f;
        }

        private void RebuildProjectilePreview()
        {
            CleanupProjectilePreview();
            EnsureProjectilePreview();
            Repaint();
        }

        private void EnsureProjectilePreview()
        {
            var projectile = SelectedProjectile;
            var prefab = projectile != null ? projectile.visualPrefab : null;
            if (_previewProjectile != projectile || _previewProjectilePrefab != prefab)
                CleanupProjectilePreview();
            _previewProjectile = projectile;
            _previewProjectilePrefab = prefab;
            if (_preview == null || prefab == null || _projectileRoot != null) return;

            _projectileRoot = new GameObject("ItemFittingProjectilePreview") { hideFlags = HideFlags.HideAndDontSave };
            _projectileRoot.SetActive(false);
            _preview.AddSingleGO(_projectileRoot);
            CreateProjectilePreviewVisual(prefab, _projectileRoot.transform, projectile);
            ApplyProjectilePreviewPose();
        }

        internal static GameObject CreateProjectilePreviewVisual(GameObject prefab, Transform parent, ProjectileDefinition definition = null)
        {
            // Preserve the prefab's authored local transform, as ProjectileFlight.Launch does.
            var visual = Instantiate(prefab, parent);
            if (definition != null) definition.ApplyVisualScale(visual.transform);
            visual.hideFlags = HideFlags.HideAndDontSave;
            foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }
            foreach (var trail in visual.GetComponentsInChildren<TrailRenderer>(true)) trail.enabled = false;
            foreach (var particles in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.playOnAwake = false;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            SetLayerRecursively(visual, 0);
            return visual;
        }

        internal static void SampleProjectilePreview(
            ProjectileDefinition projectile,
            float time,
            out Vector3 position,
            out Quaternion rotation,
            Vector3? spawnPositionOffset = null,
            Vector3? spawnRotationOffset = null)
        {
            time = Mathf.Clamp(time, 0f, Mathf.Max(0f, projectile.lifetime));
            Vector3 spawnPos = spawnPositionOffset ?? (Vector3.up * 1.3f);
            Vector3 rotEuler = spawnRotationOffset ?? Vector3.zero;
            Quaternion spawnRot = Quaternion.Euler(rotEuler);

            Vector3 launchDir = spawnRot * Vector3.forward;
            if (launchDir.sqrMagnitude < 0.0001f) launchDir = Vector3.forward;

            Vector3 velocity = launchDir.normalized * projectile.speed;
            Vector3 acceleration = Physics.gravity * projectile.gravityScale;

            position = spawnPos + velocity * time + acceleration * (0.5f * time * time);
            Vector3 currentVel = velocity + acceleration * time;

            if (time <= 0.0001f)
            {
                rotation = spawnRot;
            }
            else if (currentVel.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(currentVel);
                Quaternion initialLook = Quaternion.LookRotation(launchDir);
                Quaternion delta = Quaternion.Inverse(initialLook) * spawnRot;
                float roll = delta.eulerAngles.z;
                if (Mathf.Abs(roll) > 0.01f)
                    look *= Quaternion.Euler(0f, 0f, roll);
                rotation = look;
            }
            else
            {
                rotation = spawnRot;
            }
        }

        private void ApplyProjectilePreviewPose()
        {
            var projectile = SelectedProjectile;
            if (_projectileRoot == null || projectile == null) return;
            if (_characterInstance == null) return;
            GetPreviewSpawn(SpawnSettingsPrefab, _characterInstance, selectedWeapon.Behaviour as RangedWeaponBehaviour,
                out var spawnPos, out var spawnRot);
            SampleProjectilePreview(projectile, _projectileTime, out var point, out var rotation, spawnPos, spawnRot.eulerAngles);
            _projectileRoot.transform.SetPositionAndRotation(point, rotation);
            _projectileRoot.SetActive(_showProjectile);
            if (_followProjectile && _showProjectile) _camTarget = point;
        }

        private void UpdateProjectilePlayback(float dt)
        {
            var projectile = SelectedProjectile;
            if (!_playProjectile || projectile == null) return;
            _projectileTime = Mathf.Min(_projectileTime + dt * _playbackSpeed, projectile.lifetime);
            if (_projectileTime >= projectile.lifetime) _playProjectile = false;
            Repaint();
        }

        private void FocusOnProjectile()
        {
            if (_projectileRoot == null) return;
            var renderers = _projectileRoot.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(_projectileRoot.transform.position, Vector3.one * 0.2f);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            _camTarget = bounds.center;
            _camDistance = Mathf.Clamp(bounds.size.magnitude * 1.5f, 0.3f, 5f);
            Repaint();
        }
    }
}
#endif
