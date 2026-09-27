using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    [InitializeOnLoad]
    public static class MoonwellAnimationDiagnostic
    {
        [InitializeOnLoadMethod]
        public static void RunDiagnostic()
        {
            EditorApplication.delayCall += Diagnose;
        }

        [MenuItem("Duskborn/Art/Diagnose Moonwell Animation")]
        public static void Diagnose()
        {
            string fbxPath = "Assets/_Duskborn/Art/Models/MoonwellShrine/Models/Moonwell_Shrine.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .FirstOrDefault(a => a.name == "Moonwell_Idle");

            if (clip == null)
            {
                Debug.LogError("[MoonwellDiag] Moonwell_Idle clip NOT found in " + fbxPath);
                return;
            }

            Debug.Log($"[MoonwellDiag] Clip found: name={clip.name}, length={clip.length}s, isLooping={clip.isLooping}, empty={clip.empty}");

            var curveBindings = AnimationUtility.GetCurveBindings(clip);
            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

            Debug.Log($"[MoonwellDiag] Curve bindings count: {curveBindings.Length}, Object bindings count: {objectBindings.Length}");

            foreach (var b in curveBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                int keyCount = curve != null ? curve.keys.Length : 0;
                float minVal = float.MaxValue, maxVal = float.MinValue;
                if (curve != null && curve.keys.Length > 0)
                {
                    foreach (var k in curve.keys)
                    {
                        if (k.value < minVal) minVal = k.value;
                        if (k.value > maxVal) maxVal = k.value;
                    }
                }
                Debug.Log($"[MoonwellDiag] Binding: path='{b.path}', type={b.type.Name}, prop={b.propertyName}, keys={keyCount}, range=[{minVal} .. {maxVal}]");
            }

            // Now let's test sampling on the actual prefab
            string prefabPath = "Assets/_Duskborn/Resources/Stations/Station_ArcaneTable.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var visual = instance.transform.Find("Visual");
                    var animator = visual != null ? visual.GetComponent<Animator>() : null;
                    Debug.Log($"[MoonwellDiag] Prefab test: visual={visual != null}, animator={animator != null}, controller={animator?.runtimeAnimatorController?.name}");

                    var page = visual != null ? visual.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                    if (page != null)
                    {
                        Debug.Log($"[MoonwellDiag] Page found: {page.name}, blendShapeCount={page.sharedMesh.blendShapeCount}, skinnedMesh localBounds={page.localBounds}");
                        float initialWeight = page.GetBlendShapeWeight(0);
                        
                        // Sample at t = 1.0s
                        clip.SampleAnimation(visual.gameObject, 1.0f);
                        float sampledWeight = page.GetBlendShapeWeight(0);
                        Debug.Log($"[MoonwellDiag] Sampling Visual GameObject: shape 0 weight before={initialWeight}, after 1.0s={sampledWeight}");

                        // Also sample root GameObject
                        clip.SampleAnimation(instance, 1.0f);
                        Debug.Log($"[MoonwellDiag] Sampling Root GameObject: shape 0 weight after 1.0s={page.GetBlendShapeWeight(0)}");
                    }
                    else
                    {
                        Debug.LogError("[MoonwellDiag] SkinnedMeshRenderer for page not found in prefab!");
                    }

                    // Check book hover
                    var book = visual != null ? visual.transform.Find("Moonwell_Book") ?? visual.transform.Find("Moonwell_ROOT/Moonwell_Book") : null;
                    if (book != null)
                    {
                        Vector3 pos0 = book.localPosition;
                        clip.SampleAnimation(visual.gameObject, 2.0f);
                        Vector3 pos2 = book.localPosition;
                        Debug.Log($"[MoonwellDiag] Book '{book.name}' pos t=0: {pos0}, t=2: {pos2}");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
