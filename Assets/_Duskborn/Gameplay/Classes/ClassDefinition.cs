using UnityEngine;

namespace Duskborn.Gameplay.Classes
{
    /// <summary>
    /// Data asset defining a class's base stats. Assign one to PlayerClass.
    /// Abilities live in separate ClassAbility components — this is pure numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "Class_Name", menuName = "Duskborn/Class Definition")]
    public class ClassDefinition : ScriptableObject
    {
        public string ClassName;

        [Header("Base Stats")]
        public float MaxHP        = 100f;
        public float MoveSpeed    = 5f;
        public float Damage       = 10f;
        public float AttackSpeed  = 1f;
    }
}
