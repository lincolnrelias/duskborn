namespace Duskborn.Gameplay.Combat
{
    [System.Flags]
    public enum StatusEffect
    {
        None         = 0,
        Invulnerable = 1 << 0,
        Burn         = 1 << 1,
        Slow         = 1 << 2,
        Stun         = 1 << 3,
        Bleed        = 1 << 4,
    }
}
