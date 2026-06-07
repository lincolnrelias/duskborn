namespace Duskborn.Gameplay
{
    // Optional capability for IDamageable targets that belong to creature/material categories
    // (enemies, resource nodes). Weapons use this to look up type-based damage modifiers.
    public interface ITypedTarget
    {
        TargetType Types { get; }
    }
}
