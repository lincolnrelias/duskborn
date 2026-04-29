using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Duskborn.Network
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        private int _nextIndex;

        public override void OnStartServer()
        {
            base.OnStartServer();
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Started) return;
            if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

            int index = Mathf.Clamp(_nextIndex, 0, spawnPoints.Length - 1);
            _nextIndex++;

            Transform point = spawnPoints[index];
            GameObject go = Instantiate(playerPrefab, point.position, point.rotation);
            InstanceFinder.ServerManager.Spawn(go, conn);
        }
    }
}
