using UnityEngine;

namespace Duskborn.Audio
{
    public static class AudioExtensions
    {
        public static AudioClip RandomOrNull(this AudioClip[] clips) =>
            clips is { Length: > 0 } ? clips[Random.Range(0, clips.Length)] : null;
    }
}
