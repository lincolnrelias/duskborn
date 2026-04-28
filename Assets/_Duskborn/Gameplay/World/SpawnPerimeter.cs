using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Defines the map perimeter from which enemies spawn each night.
    /// Points are distributed evenly around a rectangular boundary.
    /// </summary>
    public class SpawnPerimeter : MonoBehaviour
    {
        [SerializeField] private float mapHalfWidth = 60f;
        [SerializeField] private float mapHalfHeight = 60f;
        [SerializeField] private int spawnPointCount = 32;

        private Vector3[] _spawnPoints;

        private void Awake() => GeneratePoints();

        private void GeneratePoints()
        {
            _spawnPoints = new Vector3[spawnPointCount];
            float perimeter = 2f * (mapHalfWidth + mapHalfHeight) * 2f;
            float step = perimeter / spawnPointCount;

            float w = mapHalfWidth, h = mapHalfHeight;

            for (int i = 0; i < spawnPointCount; i++)
            {
                float t = i * step;
                Vector3 p;

                if (t < 2 * w)
                    p = new Vector3(-w + t, 0, -h);
                else if (t < 2 * w + 2 * h)
                    p = new Vector3(w, 0, -h + (t - 2 * w));
                else if (t < 4 * w + 2 * h)
                    p = new Vector3(w - (t - 2 * w - 2 * h), 0, h);
                else
                    p = new Vector3(-w, 0, h - (t - 4 * w - 2 * h));

                _spawnPoints[i] = p;
            }
        }

        public Vector3 GetRandomSpawnPoint(SeededRNG rng)
        {
            int idx = rng != null
                ? rng.Range(0, _spawnPoints.Length)
                : Random.Range(0, _spawnPoints.Length);
            // Small random offset so enemies don't stack exactly
            float offset = 2f;
            float ox = rng != null ? rng.Range(-offset, offset) : Random.Range(-offset, offset);
            float oz = rng != null ? rng.Range(-offset, offset) : Random.Range(-offset, offset);
            return _spawnPoints[idx] + new Vector3(ox, 0, oz);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(mapHalfWidth * 2, 0.1f, mapHalfHeight * 2));
        }
    }
}
