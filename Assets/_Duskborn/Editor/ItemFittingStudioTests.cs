#if UNITY_EDITOR
using System;
using Duskborn.Gameplay.Equipment;
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
