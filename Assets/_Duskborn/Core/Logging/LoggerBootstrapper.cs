using UnityEngine;

namespace Duskborn
{
    public static class LoggerBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var settings = Resources.Load<LoggerSettings>("LoggerSettings");
            if (settings != null)
                DuskLog.Configure(settings.enabledChannels);
        }
    }
}
