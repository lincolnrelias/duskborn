using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Reusable profile defining the attachment bone (socket) and offsets
    /// for position, rotation, and scale of items and weapons equipped on the player.
    /// </summary>
    [CreateAssetMenu(fileName = "AttachmentProfile_", menuName = "Duskborn/Equipment/Attachment Profile")]
    public class ItemAttachmentProfile : ScriptableObject
    {
        [Tooltip("Humanoid bone where the item attaches by default.")]
        [SerializeField] private HumanBodyBones bone = HumanBodyBones.RightHand;

        [Tooltip("Local position offset relative to the bone / socket.")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        [Tooltip("Local Euler rotation relative to the bone / socket.")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Tooltip("Local item scale when attached.")]
        [SerializeField] private Vector3 scale = Vector3.one;

        public HumanBodyBones Bone => bone;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 Scale => scale;

        public void SetOffsets(Vector3 pos, Vector3 rotEuler, Vector3 scl, HumanBodyBones targetBone)
        {
            positionOffset = pos;
            rotationOffset = rotEuler;
            scale = scl;
            bone = targetBone;
        }

        public void ApplyToTransform(Transform target)
        {
            if (target == null) return;
            target.localPosition = positionOffset;
            target.localRotation = Quaternion.Euler(rotationOffset);
            target.localScale = scale;
        }
    }
}
