using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    /// <summary>Local visual configuration for the modular Ironroot mesh.
    /// A future character creator/network profile can drive Apply; no material instances are allocated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IronrootAppearance : MonoBehaviour
    {
        [SerializeField] private Color skin = new Color(.58f, .365f, .235f);
        [SerializeField] private Color hair = new Color(.075f, .047f, .03f);
        [SerializeField] private Color shirt = new Color(.115f, .145f, .18f);
        [SerializeField] private Color trousers = new Color(.245f, .222f, .177f);
        [SerializeField] private bool showHair = true;
        private SkinnedMeshRenderer[] _renderers;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake() => Apply(skin, hair, shirt, trousers, showHair);

        // Preserve existing equipment profiles authored against the old character's bone axes.
        public static Transform EquipmentBone(Animator animator, HumanBodyBones bone)
        {
            if (animator == null || !animator.isHuman) return null;
            var transform = animator.GetBoneTransform(bone);
            if (transform == null) return null;
            var socket = transform.Find("EquipmentSocket");
            return socket != null ? socket : transform;
        }

        public void Apply(Color skinColor, Color hairColor, Color shirtColor, Color trouserColor, bool hairVisible)
        {
            skin = skinColor; hair = hairColor; shirt = shirtColor; trousers = trouserColor; showHair = hairVisible;
            if (_renderers == null) _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_block == null) _block = new MaterialPropertyBlock();
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                if (renderer.name == "IR_Hair") renderer.enabled = showHair;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Color color;
                    switch (materials[i].name)
                    {
                        case "IR_Skin": color = skin; break;
                        case "IR_Hair":
                            // Boot soles use the same dark base material, but aren't hair.
                            if (renderer.name != "IR_Head" && renderer.name != "IR_Hair") continue;
                            color = hair; break;
                        case "IR_Shirt": color = shirt; break;
                        case "IR_Trousers": color = trousers; break;
                        default: continue;
                    }
                    _block.Clear();
                    renderer.GetPropertyBlock(_block, i);
                    _block.SetColor(BaseColor, color);
                    renderer.SetPropertyBlock(_block, i);
                }
            }
        }
    }
}
