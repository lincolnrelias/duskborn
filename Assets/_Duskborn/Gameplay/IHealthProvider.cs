using System;

namespace Duskborn.Gameplay
{
    public interface IHealthProvider
    {
        float CurrentHP { get; }
        float MaxHP     { get; }

        // Fires on all clients via SyncVar callbacks. (currentHP, maxHP)
        event Action<float, float> OnHealthChanged;
    }
}
