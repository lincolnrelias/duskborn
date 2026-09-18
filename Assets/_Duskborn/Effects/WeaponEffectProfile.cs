using UnityEngine;

namespace Duskborn.Effects
{
    [CreateAssetMenu(menuName = "Duskborn/Effects/Weapon Effect Profile")]
    public class WeaponEffectProfile : ScriptableObject
    {
        [SerializeField] public SurfaceEffectEntry[] surfaces;

        public GameObject PickEffect(string tag)
        {
            if (surfaces == null || surfaces.Length == 0) return null;
            GameObject defaultEffect = null;
            foreach (var e in surfaces)
            {
                if (string.Equals(e.tag, tag, System.StringComparison.OrdinalIgnoreCase))
                    return e.prefab;
                if (string.Equals(e.tag, "Default", System.StringComparison.OrdinalIgnoreCase))
                    defaultEffect = e.prefab;
            }
            return defaultEffect ?? surfaces[0].prefab;
        }
    }

    [System.Serializable]
    public class SurfaceEffectEntry
    {
        public string     tag;
        public GameObject prefab; // root must have a ParticleSystem
    }
}
