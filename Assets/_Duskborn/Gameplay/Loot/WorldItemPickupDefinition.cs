using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    /// <summary>Self-contained pickup definition lookup for remote clients and player builds.</summary>
    [RequireComponent(typeof(WorldItemPickup))]
    public sealed class WorldItemPickupDefinition : MonoBehaviour
    {
        [SerializeField] private ItemDefinitionBase definition;
        private void Awake() { if (definition != null) WorldDropRegistry.Instance.Register(definition); }
    }
}
