using System.Diagnostics;
using UnityEngine;

namespace Duskborn
{
    public static class DuskLog
    {
        static LogChannelMask _enabled = LogChannelMask.All;

        internal static void Configure(LogChannelMask mask) => _enabled = mask;

        public static void SetChannel(LogChannel ch, bool on)
        {
            var bit = (LogChannelMask)(1 << (int)ch);
            if (on) _enabled |= bit;
            else    _enabled &= ~bit;
        }

        static bool IsEnabled(LogChannel ch) =>
            (_enabled & (LogChannelMask)(1 << (int)ch)) != 0;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(LogChannel ch, string msg)
        {
            if (IsEnabled(ch))
                UnityEngine.Debug.Log($"[{ch}] {msg}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(LogChannel ch, string msg)
        {
            if (IsEnabled(ch))
                UnityEngine.Debug.LogWarning($"[{ch}] {msg}");
        }

        public static void Error(LogChannel ch, string msg)
        {
            if (IsEnabled(ch))
                UnityEngine.Debug.LogError($"[{ch}] {msg}");
        }
    }
}
