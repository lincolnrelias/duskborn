using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Classes
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerClass : MonoBehaviour
    {
        [SerializeField] private ClassDefinition definition;

        private void Start()
        {
            if (definition == null)
            {
                Debug.LogWarning("[PlayerClass] No ClassDefinition assigned.");
                return;
            }

            GetComponent<PlayerStats>().SetBaseStats(
                definition.MaxHP,
                definition.MoveSpeed,
                definition.Damage,
                definition.AttackSpeed);

            Debug.Log($"[PlayerClass] Applied: {definition.ClassName}");
        }
    }
}
