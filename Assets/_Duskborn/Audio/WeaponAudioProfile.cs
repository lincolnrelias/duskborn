using UnityEngine;

namespace Duskborn.Audio
{
    [CreateAssetMenu(menuName = "Duskborn/Audio/Weapon Audio Profile")]
    public class WeaponAudioProfile : ScriptableObject
    {
        [SerializeField] public AudioClip[]         swingClips;
        [SerializeField] public SurfaceAudioEntry[] surfaces;

        public AudioClip PickSwing() => swingClips.RandomOrNull();

        public AudioClip PickHit(string tag)
        {
            AudioClip[] found = null;
            foreach (var e in surfaces)
            {
                if (e.tag == tag)       { found = e.clips; break; }
                if (e.tag == "Default")   found ??= e.clips;
            }
            return found?.RandomOrNull();
        }
    }

    [System.Serializable]
    public class SurfaceAudioEntry
    {
        public string      tag;
        public AudioClip[] clips;
    }
}
