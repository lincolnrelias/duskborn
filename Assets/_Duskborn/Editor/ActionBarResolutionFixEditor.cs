using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Duskborn.Gameplay.ActionBar;

namespace Duskborn.Editor
{
    [InitializeOnLoad]
    public static class ActionBarResolutionFixEditor
    {
        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            EditorApplication.delayCall += ApplyFixToActiveScene;
        }

        static ActionBarResolutionFixEditor()
        {
            EditorApplication.delayCall += ApplyFixToActiveScene;
        }

        [MenuItem("Duskborn/UI/Sync ActionBar With Inventory", false, 100)]
        public static void ApplyFixToActiveScene()
        {
            if (Application.isPlaying) return;

            var installer = Object.FindAnyObjectByType<ActionBarInstaller>();
            if (installer != null)
            {
                installer.ConfigureCanvasAndLayout();
                EditorUtility.SetDirty(installer);

                var canvas = installer.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    EditorUtility.SetDirty(canvas);
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler != null) EditorUtility.SetDirty(scaler);
                }

                if (installer.gameObject.scene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(installer.gameObject.scene);
                }

                Debug.Log("<color=#55FF55><b>[ActionBarResolutionFixEditor] ActionBar sincronizada com sucesso com o Canvas do Inventário (800x600)!</b></color>");
            }

            bool sceneDirty = false;
            // Remove quaisquer objetos temporários de teste deixados acidentalmente na cena
            string[] testNames = new string[] { "Test_ScreenCanvas", "Test_CraftingManager", "Test_WorldCanvas", "Test_ActionBarCanvas", "Test_Canvas", "Test_Panel", "Test_ActionBarInstaller", "Test_Installer", "Test_Grid", "Test_ActionBarRoot" };
            foreach (var name in testNames)
            {
                var leaked = GameObject.Find(name);
                while (leaked != null)
                {
                    sceneDirty = true;
                    Object.DestroyImmediate(leaked);
                    leaked = GameObject.Find(name);
                }
            }

            // Remove CraftingFrame caso tenha sido gerado em Edit Mode fora do Play Mode
            if (!Application.isPlaying)
            {
                var leakedFrame = GameObject.Find("CraftingFrame");
                while (leakedFrame != null)
                {
                    sceneDirty = true;
                    Object.DestroyImmediate(leakedFrame);
                    leakedFrame = GameObject.Find("CraftingFrame");
                }

                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.isLoaded)
                {
                    foreach (var rootGO in scene.GetRootGameObjects())
                    {
                        if (rootGO != null && rootGO.name.StartsWith("Test_"))
                        {
                            sceneDirty = true;
                            Object.DestroyImmediate(rootGO);
                        }
                    }
                }
            }

            if (sceneDirty)
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                }
            }
        }
    }
}
