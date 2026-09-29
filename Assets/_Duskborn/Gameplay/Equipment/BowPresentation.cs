using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    // Local presentation only: the server owns the released projectile.
    public sealed class BowPresentation : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public Material cordMaterial;
        private WeaponActionPlayer actionPlayer;
        private GameObject arrow;
        private LineRenderer cord;

        private void Start()
        {
            actionPlayer = GetComponentInParent<WeaponActionPlayer>();
            if (actionPlayer == null) return; // Dropped-item display keeps the static cord.
            var staticCord = transform.Find("BowCord");
            if (staticCord != null) staticCord.gameObject.SetActive(false);
            cord = gameObject.AddComponent<LineRenderer>();
            cord.useWorldSpace = false;
            cord.positionCount = 3;
            cord.widthMultiplier = 0.005f;
            cord.sharedMaterial = cordMaterial;
            if (arrowPrefab != null) arrow = Instantiate(arrowPrefab, transform);
        }

        private void LateUpdate()
        {
            if (cord == null || actionPlayer == null) return;
            bool playing = actionPlayer.IsPlaying && actionPlayer.CurrentWeapon?.Behaviour is RangedWeaponBehaviour;
            float release = 0.67418647f;
            if (playing && RangedWeaponBehaviour.TryGetTiming(actionPlayer.CurrentWeapon.Actions, out float seconds, out float duration))
                release = seconds / Mathf.Max(duration, 0.001f);
            float time = actionPlayer.NormalizedTime;
            bool drawing = playing && time < release;
            float pull = drawing ? Mathf.SmoothStep(0f, 0.27f, time / Mathf.Max(release, 0.001f)) : 0f;
            cord.SetPosition(0, new Vector3(0, -0.68f, -0.22f));
            cord.SetPosition(1, new Vector3(0, 0, -0.22f - pull));
            cord.SetPosition(2, new Vector3(0, 0.68f, -0.22f));
            if (arrow != null)
            {
                arrow.SetActive(drawing);
                arrow.transform.localPosition = new Vector3(0, 0, 0.59f - pull);
                arrow.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
