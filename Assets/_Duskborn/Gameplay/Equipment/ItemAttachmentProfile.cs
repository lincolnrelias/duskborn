using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Perfil reutilizável que define o osso de acoplamento (socket) e os deslocamentos
    /// de posição, rotação e escala para itens e armas equipados no jogador.
    /// </summary>
    [CreateAssetMenu(fileName = "AttachmentProfile_", menuName = "Duskborn/Equipment/Attachment Profile")]
    public class ItemAttachmentProfile : ScriptableObject
    {
        [Tooltip("Osso humanoide onde o item será acoplado por padrão.")]
        [SerializeField] private HumanBodyBones bone = HumanBodyBones.RightHand;

        [Tooltip("Deslocamento de posição local relativo ao osso/socket.")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        [Tooltip("Rotação local em ângulos de Euler relativa ao osso/socket.")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Tooltip("Escala local do item quando acoplado.")]
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
