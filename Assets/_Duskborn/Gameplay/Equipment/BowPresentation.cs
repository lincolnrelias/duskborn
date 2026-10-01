using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Equipment
{
    // Local presentation only: the server owns the released projectile.
    [DefaultExecutionOrder(100)] // Follow the final bow spine correction in LateUpdate.
    public sealed class BowPresentation : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public Material cordMaterial;
        private WeaponActionPlayer actionPlayer;
        private PlayerCombat combat;
        private GameObject arrow;
        private GameObject activeArrowPrefab;
        private LineRenderer cord;

        private void Start()
        {
            actionPlayer = GetComponentInParent<WeaponActionPlayer>();
            if (actionPlayer == null) return; // Dropped-item display keeps the static cord.
            combat = actionPlayer.GetComponent<PlayerCombat>();
            var staticCord = transform.Find("BowCord");
            if (staticCord != null) staticCord.gameObject.SetActive(false);
            cord = gameObject.AddComponent<LineRenderer>();
            cord.useWorldSpace = false;
            cord.positionCount = 3;
            cord.widthMultiplier = 0.005f;
            cord.sharedMaterial = cordMaterial;
        }

        private void LateUpdate()
        {
            if (cord == null || actionPlayer == null) return;
            bool playing = actionPlayer.IsPlaying && actionPlayer.CurrentWeapon?.Behaviour is RangedWeaponBehaviour;
            bool drawing = playing && actionPlayer.IsDrawingBow;
            var projectile = (actionPlayer.CurrentWeapon?.Behaviour as RangedWeaponBehaviour)?.projectile;
            var visualPrefab = projectile != null ? projectile.visualPrefab : arrowPrefab;
            if (activeArrowPrefab != visualPrefab)
            {
                if (arrow != null)
                {
                    arrow.SetActive(false);
                    Destroy(arrow);
                }
                activeArrowPrefab = visualPrefab;
                arrow = visualPrefab != null ? Instantiate(visualPrefab, transform) : null;
            }
            if (arrow != null && projectile != null) projectile.ApplyVisualScale(arrow.transform);
            else if (arrow != null && visualPrefab != null) arrow.transform.localScale = visualPrefab.transform.localScale;
            float pull = drawing ? Mathf.SmoothStep(0f, 0.27f, actionPlayer.RangedDrawProgress) : 0f;
            var hand = actionPlayer.RangedDrawingHand;
            cord.SetPosition(0, new Vector3(0, -0.68f, -0.22f));
            cord.SetPosition(1, drawing && hand != null
                ? transform.InverseTransformPoint(hand.position) : new Vector3(0, 0, -0.22f - pull));
            cord.SetPosition(2, new Vector3(0, 0.68f, -0.22f));
            if (arrow != null)
            {
                arrow.SetActive(drawing);
                arrow.transform.localPosition = new Vector3(0, 0, 0.59f - pull);
                arrow.transform.localRotation = Quaternion.identity;
                if (drawing && hand != null && projectile != null)
                {
                    Quaternion rotation = actionPlayer.RangedAimRotation;
                    Vector3 position = projectile.TipPositionFromNock(hand.position, rotation);
                    if (combat != null)
                        combat.GetRangedSpawn(actionPlayer.CurrentWeapon, out position, out rotation);
                    arrow.transform.SetPositionAndRotation(position, rotation);
                }
            }
        }
    }
}
