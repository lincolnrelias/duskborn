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

        public void SetSpawnPoints(Transform[] points)
        {
            spawnPoints = points;
        }

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

            if (spawnPoints == null || spawnPoints.Length == 0 || spawnPoints[0] == null)
            {
                GameObject spawnGroup = GameObject.Find("SpawnPoints");
                if (spawnGroup != null && spawnGroup.transform.childCount > 0)
                {
                    System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();
                    for (int i = 0; i < spawnGroup.transform.childCount; i++)
                    {
                        Transform child = spawnGroup.transform.GetChild(i);
                        if (child != null) list.Add(child);
                    }
                    if (list.Count > 0) spawnPoints = list.ToArray();
                }
            }

            if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

            int index = Mathf.Clamp(_nextIndex, 0, spawnPoints.Length - 1);
            _nextIndex++;

            Transform point = spawnPoints[index];
            GameObject go = Instantiate(playerPrefab, point.position, point.rotation);
            InstanceFinder.ServerManager.Spawn(go, conn);
        }
    }
}
