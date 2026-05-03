using UnityEngine;

namespace Duskborn
{
    [CreateAssetMenu(fileName = "LoggerSettings", menuName = "Duskborn/Logger Settings")]
    public class LoggerSettings : ScriptableObject
    {
        [Tooltip("Channels checked here will emit logs. Uncheck to silence a channel.")]
        public LogChannelMask enabledChannels = LogChannelMask.All;
    }
}
