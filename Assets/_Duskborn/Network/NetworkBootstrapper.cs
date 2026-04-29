using FishNet;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
#endif

namespace Duskborn.Network
{
    public class NetworkBootstrapper : MonoBehaviour
    {
        [SerializeField] private ushort port = 7770;

        private void Start()
        {
            bool isClone = false;
#if UNITY_EDITOR
            isClone = ClonesManager.IsClone();
#endif
            if (isClone)
            {
                InstanceFinder.ClientManager.StartConnection("localhost", port);
            }
            else
            {
                InstanceFinder.ServerManager.StartConnection(port);
                InstanceFinder.ClientManager.StartConnection("localhost", port);
            }
        }
    }
}
