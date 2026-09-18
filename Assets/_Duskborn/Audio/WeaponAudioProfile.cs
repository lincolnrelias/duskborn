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
            if (string.IsNullOrEmpty(tag)) tag = "Default";
            AudioClip[] found = null;
            AudioClip[] defaultClips = null;

            foreach (var e in surfaces)
            {
                if (string.Equals(e.tag, tag, System.StringComparison.OrdinalIgnoreCase))
                    return e.clips?.RandomOrNull();

                if (string.Equals(e.tag, "Default", System.StringComparison.OrdinalIgnoreCase))
                    defaultClips = e.clips;

                // Alias checks if exact match not yet found
                if (found == null)
                {
                    if ((tag.Equals("Ore", System.StringComparison.OrdinalIgnoreCase) && e.tag.Equals("Metal", System.StringComparison.OrdinalIgnoreCase)) ||
                        (tag.Equals("Metal", System.StringComparison.OrdinalIgnoreCase) && e.tag.Equals("Ore", System.StringComparison.OrdinalIgnoreCase)))
                        found = e.clips;
                    else if ((tag.Equals("Wood", System.StringComparison.OrdinalIgnoreCase) && e.tag.Equals("Tree", System.StringComparison.OrdinalIgnoreCase)) ||
                             (tag.Equals("Tree", System.StringComparison.OrdinalIgnoreCase) && e.tag.Equals("Wood", System.StringComparison.OrdinalIgnoreCase)))
                        found = e.clips;
                    else if ((tag.Equals("Rock", System.StringComparison.OrdinalIgnoreCase) && e.tag.Equals("Stone", System.StringComparison.OrdinalIgnoreCase)) ||
                             (tag.Equals("Stone", System.StringComparison.OrdinalIgnoreCase) && e.tag.Equals("Rock", System.StringComparison.OrdinalIgnoreCase)))
                        found = e.clips;
                }
            }

            return (found ?? defaultClips)?.RandomOrNull();
        }
    }

    [System.Serializable]
    public class SurfaceAudioEntry
    {
        public string      tag;
        public AudioClip[] clips;
    }
}
