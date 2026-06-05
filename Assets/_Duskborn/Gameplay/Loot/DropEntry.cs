using System;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    [Serializable]
    public class DropEntry
    {
        public ItemDefinitionBase itemDefinition;
        [Range(0f, 1f)]  public float baseChance    = 0.25f;
        [Range(-1f, 1f)] public float scalingBonus  = 0f;    // positive → improves with nights
        public int minAmount = 1;
        public int maxAmount = 1;
    }
}
