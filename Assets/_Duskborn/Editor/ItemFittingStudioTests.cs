#if UNITY_EDITOR
using System;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Projectiles;
using Duskborn.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    /// <summary>
    /// Testes automatizados para o sistema de perfis de acoplamento (ItemAttachmentProfile)
    /// e integração com WeaponDefinition e WeaponItem.
    /// </summary>
    public static class ItemFittingStudioTests
    {
        [MenuItem("Duskborn/Tests/Executar Testes de Ajuste de Itens", false, 115)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_AttachmentProfile_SetAndApplyOffsets, ref passed, ref total);
            RunTest(Test_WeaponDefinition_AttachmentProfileIntegration, ref passed, ref total);
            RunTest(Test_WeaponItem_CarriesAttachmentProfile, ref passed, ref total);
            RunTest(Test_ItemFittingStudio_StatePersistence, ref passed, ref total);
            RunTest(Test_AllProjectWeapons_HaveRenderablePrefabs, ref passed, ref total);
            RunTest(Test_ProjectilePreview_BallisticsAndLifetime, ref passed, ref total);
            RunTest(Test_ProjectilePreview_IsolatesVisual, ref passed, ref total);
            RunTest(Test_ProjectilePreview_CustomSpawnOffsets, ref passed, ref total);
            RunTest(Test_ProjectilePreview_ReadsCurrentSpawnSettings, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[ItemFittingStudioTests] {passed}/{total} testes passaram com sucesso!</b></color>");
        }

        private static void RunTest(Action testMethod, ref int passed, ref int total)
        {
            total++;
            try
            {
                testMethod();
                passed++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemFittingStudioTests] FALHA em {testMethod.Method.Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Test_AttachmentProfile_SetAndApplyOffsets()
        {
            var profile = ScriptableObject.CreateInstance<ItemAttachmentProfile>();
            Vector3 testPos = new Vector3(0.05f, -0.1f, 0.25f);
            Vector3 testRot = new Vector3(15f, 45f, -90f);
            Vector3 testScale = new Vector3(1.5f, 1.5f, 1.5f);
            HumanBodyBones testBone = HumanBodyBones.LeftHand;

            profile.SetOffsets(testPos, testRot, testScale, testBone);

            if (profile.Bone != testBone)
                throw new Exception($"Bone esperado: {testBone}, obtido: {profile.Bone}");
            if (profile.PositionOffset != testPos)
                throw new Exception($"PositionOffset esperado: {testPos}, obtido: {profile.PositionOffset}");
            if (profile.RotationOffset != testRot)
                throw new Exception($"RotationOffset esperado: {testRot}, obtido: {profile.RotationOffset}");
            if (profile.Scale != testScale)
                throw new Exception($"Scale esperado: {testScale}, obtido: {profile.Scale}");

            var dummyObj = new GameObject("DummyTestWeapon");
            try
            {
                profile.ApplyToTransform(dummyObj.transform);

                if (Vector3.Distance(dummyObj.transform.localPosition, testPos) > 0.001f)
                    throw new Exception("Falha ao aplicar localPosition.");
                if (Quaternion.Angle(dummyObj.transform.localRotation, Quaternion.Euler(testRot)) > 0.01f)
                    throw new Exception("Falha ao aplicar localRotation.");
                if (Vector3.Distance(dummyObj.transform.localScale, testScale) > 0.001f)
                    throw new Exception("Falha ao aplicar localScale.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dummyObj);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static void Test_WeaponDefinition_AttachmentProfileIntegration()
        {
            var weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
            var profile = ScriptableObject.CreateInstance<ItemAttachmentProfile>();

            try
            {
                weaponDef.SetAttachmentProfile(profile);
                if (weaponDef.AttachmentProfile != profile)
                    throw new Exception("AttachmentProfile não foi atribuído corretamente no WeaponDefinition.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(weaponDef);
            }
        }

        private static void Test_WeaponItem_CarriesAttachmentProfile()
        {
            var weaponDef = ScriptableObject.CreateInstance<WeaponDefinition>();
            var so = new SerializedObject(weaponDef);
            var idProp = so.FindProperty("id");
            if (idProp != null) idProp.stringValue = "test_weapon_id";
            so.ApplyModifiedPropertiesWithoutUndo();

            var profile = ScriptableObject.CreateInstance<ItemAttachmentProfile>();
            profile.SetOffsets(new Vector3(1, 2, 3), Vector3.zero, Vector3.one, HumanBodyBones.RightHand);

            try
            {
                weaponDef.SetAttachmentProfile(profile);
                var runtimeItem = weaponDef.CreateRuntimeItem() as WeaponItem;

                if (runtimeItem == null)
                    throw new Exception("CreateRuntimeItem() não retornou um WeaponItem.");
                if (runtimeItem.AttachmentProfile != profile)
                    throw new Exception("WeaponItem em tempo de execução não contém a referência do AttachmentProfile.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(weaponDef);
            }
        }

        private static void Test_ItemFittingStudio_StatePersistence()
        {
            var originalState = new ItemFittingStudioWindow.WindowSavedState
            {
                weaponGuid = "dummy_weapon_guid_123",
                profileGuid = "dummy_profile_guid_456",
                camTarget = new Vector3(1.2f, 3.4f, 5.6f),
                camYaw = 180f,
                camPitch = 25f,
                camDistance = 3.5f,
                darkBackground = true,
                lightIntensity = 2.0f,
                targetBone = (int)HumanBodyBones.LeftUpperArm,
                positionOffset = new Vector3(0.1f, -0.2f, 0.3f),
                rotationOffset = new Vector3(10f, 20f, 30f),
                scaleOffset = new Vector3(1.2f, 1.2f, 1.2f),
                uniformScale = true,
                selectedClipIndex = 3,
                loopAnimation = false,
                playbackSpeed = 0.5f,
                currentTime = 1.25f,
                normalizedTime = 0.65f,
                sidebarScroll = new Vector2(10f, 40f)
            };

            string json = JsonUtility.ToJson(originalState);
            if (string.IsNullOrEmpty(json))
                throw new Exception("Falha ao serializar WindowSavedState para JSON.");

            var restoredState = JsonUtility.FromJson<ItemFittingStudioWindow.WindowSavedState>(json);
            if (restoredState == null)
                throw new Exception("Falha ao desserializar WindowSavedState do JSON.");

            if (restoredState.weaponGuid != originalState.weaponGuid)
                throw new Exception("weaponGuid não corresponde.");
            if (restoredState.profileGuid != originalState.profileGuid)
                throw new Exception("profileGuid não corresponde.");
            if (Vector3.Distance(restoredState.camTarget, originalState.camTarget) > 0.001f)
                throw new Exception("camTarget não corresponde.");
            if (Mathf.Abs(restoredState.camYaw - originalState.camYaw) > 0.001f)
                throw new Exception("camYaw não corresponde.");
            if (restoredState.targetBone != originalState.targetBone)
                throw new Exception("targetBone não corresponde.");
            if (Vector3.Distance(restoredState.positionOffset, originalState.positionOffset) > 0.001f)
                throw new Exception("positionOffset não corresponde.");
            if (Vector3.Distance(restoredState.rotationOffset, originalState.rotationOffset) > 0.001f)
                throw new Exception("rotationOffset não corresponde.");
            if (Vector3.Distance(restoredState.scaleOffset, originalState.scaleOffset) > 0.001f)
                throw new Exception("scaleOffset não corresponde.");
            if (restoredState.darkBackground != originalState.darkBackground)
                throw new Exception("darkBackground não corresponde.");
            if (restoredState.selectedClipIndex != originalState.selectedClipIndex)
                throw new Exception("selectedClipIndex não corresponde.");
            if (Mathf.Abs(restoredState.playbackSpeed - originalState.playbackSpeed) > 0.001f)
                throw new Exception("playbackSpeed não corresponde.");
            if (Mathf.Abs(restoredState.currentTime - originalState.currentTime) > 0.001f)
                throw new Exception("currentTime não corresponde.");
        }

        private static void Test_ProjectilePreview_BallisticsAndLifetime()
        {
            var projectile = ScriptableObject.CreateInstance<ProjectileDefinition>();
            try
            {
                projectile.speed = 12f;
                projectile.gravityScale = 0f;
                projectile.lifetime = 2f;
                ItemFittingStudioWindow.SampleProjectilePreview(projectile, 1f, out var position, out var rotation);
                if (Vector3.Distance(position, new Vector3(0f, 1.3f, 12f)) > 0.001f ||
                    Quaternion.Angle(rotation, Quaternion.identity) > 0.01f)
                    throw new Exception("Zero-gravity projectile preview must travel straight along +Z.");

                projectile.gravityScale = 1.5f;
                ItemFittingStudioWindow.SampleProjectilePreview(projectile, 10f, out position, out rotation);
                var expected = new Vector3(0f, 1.3f, 24f) + Physics.gravity * 3f;
                var direction = Vector3.forward * 12f + Physics.gravity * 3f;
                if (Vector3.Distance(position, expected) > 0.001f ||
                    Vector3.Angle(rotation * Vector3.forward, direction) > 0.01f)
                    throw new Exception("Preview must clamp to lifetime and face its ballistic velocity.");

                ItemFittingStudioWindow.SampleProjectilePreview(projectile, -1f, out position, out rotation);
                if (Vector3.Distance(position, Vector3.up * 1.3f) > 0.001f)
                    throw new Exception("Rewinding must restore the launch position.");
            }
            finally { UnityEngine.Object.DestroyImmediate(projectile); }
        }

        private static void Test_ProjectilePreview_IsolatesVisual()
        {
            var parent = new GameObject("ProjectilePreviewTest");
            parent.SetActive(false);
            var source = new GameObject("ProjectileVisualSource");
            source.SetActive(false);
            source.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            source.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            source.transform.localScale = Vector3.one * 0.25f;
            source.AddComponent<BoxCollider>();
            source.AddComponent<Rigidbody>();
            source.AddComponent<BowPresentation>();
            var definition = ScriptableObject.CreateInstance<ProjectileDefinition>();
            definition.visualPrefab = source;
            try
            {
                var visual = ItemFittingStudioWindow.CreateProjectilePreviewVisual(source, parent.transform, definition);
                if (visual.transform.localPosition != source.transform.localPosition ||
                    visual.transform.localScale != source.transform.localScale ||
                    Quaternion.Angle(visual.transform.localRotation, source.transform.localRotation) > 0.01f)
                    throw new Exception("Preview must preserve the projectile prefab's authored transform.");
                var body = visual.GetComponent<Rigidbody>();
                if (visual.GetComponent<BoxCollider>().enabled || visual.GetComponent<BowPresentation>().enabled ||
                    !body.isKinematic || body.useGravity || body.detectCollisions)
                    throw new Exception("Projectile previews must not run gameplay or physics.");
                if (!source.GetComponent<BoxCollider>().enabled || !source.GetComponent<BowPresentation>().enabled ||
                    source.GetComponent<Rigidbody>().isKinematic)
                    throw new Exception("Preview creation must leave its source unchanged.");
                definition.visualScale = new Vector3(2f, 3f, 4f);
                var scaledVisual = ItemFittingStudioWindow.CreateProjectilePreviewVisual(source, parent.transform, definition);
                var expectedScale = new Vector3(0.5f, 0.75f, 1f);
                if (scaledVisual.transform.localScale != expectedScale)
                    throw new Exception("Preview scale must multiply the authored prefab scale per axis.");
                definition.ApplyVisualScale(scaledVisual.transform);
                if (scaledVisual.transform.localScale != expectedScale || source.transform.localScale != Vector3.one * 0.25f)
                    throw new Exception("Applying scale repeatedly must not accumulate or modify the source prefab.");
                definition.visualScale = new Vector3(4f, 4f, 4f);
                definition.ApplyVisualScale(scaledVisual.transform);
                if (scaledVisual.transform.localScale != Vector3.one)
                    throw new Exception("Dynamic visual scale updates must reflect on existing visual transforms.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static void Test_ProjectilePreview_ReadsCurrentSpawnSettings()
        {
            var source = new GameObject("SpawnSettingsSource");
            source.SetActive(false);
            GameObject instance = null;
            var ranged = ScriptableObject.CreateInstance<RangedWeaponBehaviour>();
            try
            {
                var combat = source.AddComponent<PlayerCombat>();
                var socket = new GameObject("Socket").transform;
                socket.SetParent(source.transform, false);
                socket.localPosition = new Vector3(0.2f, 1f, 0.4f);
                instance = UnityEngine.Object.Instantiate(source);
                instance.transform.SetPositionAndRotation(new Vector3(3f, 2f, 1f), Quaternion.Euler(0f, 90f, 0f));
                instance.transform.localScale = Vector3.one * 2f;

                // Editing the source after cloning must immediately affect the preview.
                combat.RangedSpawnOffset = new Vector3(0.5f, 1.6f, 0.8f);
                combat.RangedSpawnRotationOffset = new Vector3(10f, 20f, 30f);
                ItemFittingStudioWindow.GetPreviewSpawn(source, instance, ranged, out var position, out var rotation);
                if (Vector3.Distance(position, instance.transform.TransformPoint(combat.RangedSpawnOffset)) > 0.001f ||
                    Quaternion.Angle(rotation, instance.transform.rotation * Quaternion.Euler(combat.RangedSpawnRotationOffset)) > 0.01f)
                    throw new Exception("Preview must read current source settings and transform them into preview world space.");

                combat.RangedSpawnPoint = socket;
                ItemFittingStudioWindow.GetPreviewSpawn(source, instance, ranged, out position, out rotation);
                if (Vector3.Distance(position, instance.transform.Find("Socket").position) > 0.001f)
                    throw new Exception("Preview must resolve the configured socket on its own character instance.");

                ranged.useCustomSpawnOffset = true;
                ranged.spawnPositionOffset = new Vector3(-0.3f, 1.2f, 0.6f);
                ItemFittingStudioWindow.GetPreviewSpawn(source, instance, ranged, out position, out rotation);
                if (Vector3.Distance(position, instance.transform.TransformPoint(ranged.spawnPositionOffset)) > 0.001f)
                    throw new Exception("Weapon spawn overrides must take precedence over the player socket.");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(ranged);
            }
        }

        private static void Test_ProjectilePreview_CustomSpawnOffsets()
        {
            var projectile = ScriptableObject.CreateInstance<ProjectileDefinition>();
            projectile.speed = 10f;
            projectile.gravityScale = 0f;
            projectile.lifetime = 3f;

            try
            {
                var customPos = new Vector3(0.4f, 1.1f, 0.2f);
                var customRot = new Vector3(15f, 30f, 0f);

                // At time 0, spawn position and rotation must match exactly
                ItemFittingStudioWindow.SampleProjectilePreview(projectile, 0f, out var pos0, out var rot0, customPos, customRot);
                if (Vector3.Distance(pos0, customPos) > 0.001f)
                    throw new Exception($"Custom spawn position mismatch at t=0: expected {customPos}, got {pos0}");
                if (Quaternion.Angle(rot0, Quaternion.Euler(customRot)) > 0.01f)
                    throw new Exception($"Custom spawn rotation mismatch at t=0: expected {Quaternion.Euler(customRot)}, got {rot0}");

                // At time 1, arrow must travel in the direction defined by custom rotation
                ItemFittingStudioWindow.SampleProjectilePreview(projectile, 1f, out var pos1, out var rot1, customPos, customRot);
                Vector3 launchDir = Quaternion.Euler(customRot) * Vector3.forward;
                Vector3 expectedPos1 = customPos + launchDir * 10f;
                if (Vector3.Distance(pos1, expectedPos1) > 0.001f)
                    throw new Exception($"Custom trajectory position mismatch at t=1: expected {expectedPos1}, got {pos1}");
                if (Vector3.Angle(rot1 * Vector3.forward, launchDir) > 0.01f)
                    throw new Exception("Custom trajectory rotation mismatch at t=1: must point along launchDir");

                // When null is passed, default fallback (0, 1.3, 0) and forward must be used
                ItemFittingStudioWindow.SampleProjectilePreview(projectile, 0f, out var defPos, out var defRot, null, null);
                if (Vector3.Distance(defPos, Vector3.up * 1.3f) > 0.001f)
                    throw new Exception($"Default spawn position mismatch: expected (0, 1.3, 0), got {defPos}");
                if (Quaternion.Angle(defRot, Quaternion.identity) > 0.01f)
                    throw new Exception($"Default spawn rotation mismatch: expected identity, got {defRot}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectile);
            }
        }

        private static void Test_AllProjectWeapons_HaveRenderablePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:WeaponDefinition");
            if (guids.Length == 0)
                throw new Exception("Nenhum WeaponDefinition encontrado no projeto.");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (weapon == null) continue;

                if (weapon.Prefab == null)
                    throw new Exception($"WeaponDefinition em '{path}' não possui Prefab associado.");

                var renderers = weapon.Prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new Exception($"Prefab da arma '{weapon.name}' em '{path}' não possui nenhum componente Renderer.");

                if (weapon.Prefab.transform.localScale == Vector3.zero)
                    throw new Exception($"Prefab da arma '{weapon.name}' tem escala local zerada.");
            }
        }
    }
}
#endif
