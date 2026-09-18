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
        }
    }
}
