using UnityEngine;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Represents a player spawn point procedurally generated with terrain.
    /// Includes Scene View gizmos for easier visualization and debugging.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Spawn point identification index.")]
        public int spawnIndex;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.85f);
            Gizmos.DrawRay(transform.position + Vector3.up * 1.0f, transform.forward * 1.0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2.0f);
            Gizmos.DrawRay(transform.position + Vector3.up * 1.0f, transform.forward * 1.5f);
        }
    }
}
