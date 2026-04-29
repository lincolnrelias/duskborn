using System.Threading;
using FishNet;
using UnityEngine;

namespace Duskborn.Network
{
    public class NetworkBootstrapper : MonoBehaviour
    {
        [SerializeField] private ushort port = 7770;

        private Mutex _hostMutex;

        private void Start()
        {
            // First instance to boot acquires the mutex and becomes host.
            // All subsequent instances (editor clones, standalone builds) become clients.
            _hostMutex = new Mutex(true, "DuskbornHostMutex", out bool isHost);
            if (!isHost)
            {
                _hostMutex.Dispose();
                _hostMutex = null;
            }

            if (isHost)
            {
                Debug.Log("[Network] This instance is HOST");
                InstanceFinder.ServerManager.StartConnection(port);
                InstanceFinder.ClientManager.StartConnection("localhost", port);
            }
            else
            {
                Debug.Log("[Network] This instance is CLIENT");
                InstanceFinder.ClientManager.StartConnection("localhost", port);
            }
        }

        private void OnDestroy()
        {
            if (_hostMutex != null)
            {
                _hostMutex.ReleaseMutex();
                _hostMutex.Dispose();
                _hostMutex = null;
            }
        }
    }
}
