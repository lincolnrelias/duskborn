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
            GameObject found = null;
            foreach (var e in surfaces)
            {
                if (e.tag == tag)       { found = e.prefab; break; }
                if (e.tag == "Default")   found ??= e.prefab;
            }
            return found;
        }
    }

    [System.Serializable]
    public class SurfaceEffectEntry
    {
        public string     tag;
        public GameObject prefab; // root must have a ParticleSystem
    }
}
