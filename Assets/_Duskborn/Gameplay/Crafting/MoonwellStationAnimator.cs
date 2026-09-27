using UnityEngine;

namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Garante que as animações de bancadas/estações animadas (como a Mesa Arcana / Moonwell Shrine)
    /// executem continuamente sem sofrer culling por câmera ou congelamento de blend shapes.
    /// </summary>
    [DisallowMultipleComponent]
    public class MoonwellStationAnimator : MonoBehaviour
    {
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private string stateName = "Moonwell_Idle";

        private void Awake()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            EnsureConfigured();
            PlayAnimation();
        }

        private void EnsureConfigured()
        {
            if (targetAnimator == null)
                targetAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

            if (targetAnimator != null)
            {
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }
        }

        public void PlayAnimation()
        {
            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null && targetAnimator.isActiveAndEnabled)
            {
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!string.IsNullOrEmpty(stateName))
                {
                    targetAnimator.Play(stateName, 0, 0f);
                    targetAnimator.Update(0f);
                }
            }
        }
    }
}
